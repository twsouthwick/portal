use std::error::Error as StdError;
use std::time::Duration;

use reqwest::header::{ACCEPT, WWW_AUTHENTICATE};
use reqwest::{Client, StatusCode, redirect};
use serde::Serialize;

use crate::PortalOrigin;

/// The documented, unauthenticated-safe Portal endpoint used for the check.
pub const PROBE_PATH: &str = "/api/manifest";
pub const PROBE_TIMEOUT: Duration = Duration::from_secs(10);

/// Result of checking an origin. Reaching the endpoint does not establish
/// Portal identity or trust.
#[derive(Clone, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", tag = "kind")]
pub enum ProbeOutcome {
    /// `401` with a bearer challenge: the manifest endpoint answered.
    SignInRequired,
    /// `2xx` from the manifest endpoint.
    Reachable,
    Tls,
    Network,
    Timeout,
    Authentication {
        status: u16,
    },
    Redirect {
        status: u16,
    },
    UnexpectedResponse {
        status: u16,
    },
}

impl ProbeOutcome {
    pub fn found_portal(&self) -> bool {
        matches!(self, Self::SignInRequired | Self::Reachable)
    }
}

/// @spec CLIENT-010
pub fn probe_client(timeout: Duration) -> reqwest::Result<Client> {
    Client::builder()
        .redirect(redirect::Policy::none())
        .timeout(timeout)
        .build()
}

/// @spec CLIENT-010 CLIENT-011 CLIENT-012
pub async fn probe(client: &Client, origin: &PortalOrigin) -> ProbeOutcome {
    let response = client
        .get(origin.join(PROBE_PATH))
        .header(ACCEPT, "application/json")
        .send()
        .await;
    match response {
        Ok(response) => {
            classify_response(response.status(), response.headers().get(WWW_AUTHENTICATE))
        }
        Err(error) => classify_error(&error),
    }
}

fn classify_response(
    status: StatusCode,
    challenge: Option<&reqwest::header::HeaderValue>,
) -> ProbeOutcome {
    let status_code = status.as_u16();
    if status.is_success() {
        return ProbeOutcome::Reachable;
    }
    if status.is_redirection() {
        return ProbeOutcome::Redirect {
            status: status_code,
        };
    }
    match status {
        StatusCode::UNAUTHORIZED if challenge.is_some_and(is_bearer_challenge) => {
            ProbeOutcome::SignInRequired
        }
        StatusCode::UNAUTHORIZED
        | StatusCode::FORBIDDEN
        | StatusCode::PROXY_AUTHENTICATION_REQUIRED => ProbeOutcome::Authentication {
            status: status_code,
        },
        _ => ProbeOutcome::UnexpectedResponse {
            status: status_code,
        },
    }
}

fn is_bearer_challenge(value: &reqwest::header::HeaderValue) -> bool {
    value.to_str().is_ok_and(|v| {
        v.trim_start()
            .get(..6)
            .is_some_and(|s| s.eq_ignore_ascii_case("bearer"))
    })
}

fn classify_error(error: &reqwest::Error) -> ProbeOutcome {
    if error.is_timeout() {
        ProbeOutcome::Timeout
    } else if is_tls_error(error) {
        ProbeOutcome::Tls
    } else {
        ProbeOutcome::Network
    }
}

fn is_tls_error(error: &(dyn StdError + 'static)) -> bool {
    if error.is::<rustls::Error>() {
        return true;
    }
    // io::Error::source() skips the error it wraps, so inspect it directly.
    if let Some(inner) = error
        .downcast_ref::<std::io::Error>()
        .and_then(|e| e.get_ref())
        && is_tls_error(inner)
    {
        return true;
    }
    error.source().is_some_and(is_tls_error)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::OriginPolicy;
    use std::sync::Arc;
    use std::sync::atomic::{AtomicUsize, Ordering};
    use tokio::io::{AsyncReadExt, AsyncWriteExt};
    use tokio::net::TcpListener;
    use tokio::sync::Mutex;

    struct Server {
        origin: String,
        requests: Arc<AtomicUsize>,
        last_request: Arc<Mutex<String>>,
    }

    async fn serve(response: Option<&'static str>) -> Server {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let port = listener.local_addr().unwrap().port();
        let requests = Arc::new(AtomicUsize::new(0));
        let last_request = Arc::new(Mutex::new(String::new()));
        let (count, last) = (requests.clone(), last_request.clone());
        tokio::spawn(async move {
            loop {
                let Ok((mut socket, _)) = listener.accept().await else {
                    return;
                };
                count.fetch_add(1, Ordering::SeqCst);
                let mut buffer = vec![0; 8192];
                let read = socket.read(&mut buffer).await.unwrap_or(0);
                *last.lock().await = String::from_utf8_lossy(&buffer[..read]).into_owned();
                match response {
                    Some(response) => {
                        let _ = socket.write_all(response.as_bytes()).await;
                        let _ = socket.shutdown().await;
                    }
                    None => {
                        tokio::time::sleep(Duration::from_secs(30)).await;
                    }
                }
            }
        });
        Server {
            origin: format!("http://127.0.0.1:{port}"),
            requests,
            last_request,
        }
    }

    fn origin(value: &str) -> PortalOrigin {
        PortalOrigin::parse(value, OriginPolicy::AllowHttpLoopback).unwrap()
    }

    async fn check(value: &str) -> ProbeOutcome {
        probe(&probe_client(PROBE_TIMEOUT).unwrap(), &origin(value)).await
    }

    const PORTAL_401: &str = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Bearer\r\nContent-Type: application/problem+json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}";

    // @spec CLIENT-010 CLIENT-011
    #[tokio::test]
    async fn bearer_challenge_means_portal_found_and_request_has_no_credentials() {
        let server = serve(Some(PORTAL_401)).await;
        let outcome = check(&server.origin).await;
        assert_eq!(outcome, ProbeOutcome::SignInRequired);
        assert!(outcome.found_portal());

        let request = server.last_request.lock().await.to_ascii_lowercase();
        assert!(
            request.starts_with("get /api/manifest http/1.1\r\n"),
            "{request}"
        );
        assert!(!request.contains("\r\nauthorization:"), "{request}");
        assert!(!request.contains("\r\ncookie:"), "{request}");
    }

    // @spec CLIENT-011
    #[tokio::test]
    async fn success_means_reachable() {
        let server = serve(Some("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}")).await;
        assert_eq!(check(&server.origin).await, ProbeOutcome::Reachable);
    }

    // @spec CLIENT-010 CLIENT-012
    #[tokio::test]
    async fn redirects_are_reported_and_not_followed() {
        let server = serve(Some("HTTP/1.1 302 Found\r\nLocation: /login\r\nContent-Length: 0\r\nConnection: close\r\n\r\n")).await;
        assert_eq!(
            check(&server.origin).await,
            ProbeOutcome::Redirect { status: 302 }
        );
        assert_eq!(server.requests.load(Ordering::SeqCst), 1);
    }

    // @spec CLIENT-012
    #[tokio::test]
    async fn authentication_failures_are_distinct() {
        for (response, status) in [
            (
                "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"proxy\"\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
                401,
            ),
            (
                "HTTP/1.1 401 Unauthorized\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
                401,
            ),
            (
                "HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
                403,
            ),
            (
                "HTTP/1.1 407 Proxy Authentication Required\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
                407,
            ),
        ] {
            let server = serve(Some(response)).await;
            let outcome = check(&server.origin).await;
            assert_eq!(outcome, ProbeOutcome::Authentication { status });
            assert!(!outcome.found_portal());
        }
    }

    // @spec CLIENT-012
    #[tokio::test]
    async fn unexpected_status_is_reported() {
        let server = serve(Some(
            "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
        ))
        .await;
        assert_eq!(
            check(&server.origin).await,
            ProbeOutcome::UnexpectedResponse { status: 404 }
        );
    }

    // @spec CLIENT-012
    #[tokio::test]
    async fn tls_failure_is_distinct_from_network_failure() {
        let server = serve(Some(
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
        ))
        .await;
        let tls_origin = server.origin.replace("http://", "https://");
        assert_eq!(check(&tls_origin).await, ProbeOutcome::Tls);
    }

    // @spec CLIENT-012
    #[tokio::test]
    async fn connection_failure_is_network() {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let port = listener.local_addr().unwrap().port();
        drop(listener);
        assert_eq!(
            check(&format!("http://127.0.0.1:{port}")).await,
            ProbeOutcome::Network
        );
    }

    // @spec CLIENT-010 CLIENT-012
    #[tokio::test]
    async fn unresponsive_server_times_out() {
        let server = serve(None).await;
        let client = probe_client(Duration::from_millis(200)).unwrap();
        assert_eq!(
            probe(&client, &origin(&server.origin)).await,
            ProbeOutcome::Timeout
        );
    }
}
