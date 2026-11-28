use std::error::Error as StdError;
use std::time::Duration;

use reqwest::header::{ACCEPT, WWW_AUTHENTICATE};
use reqwest::{Client, StatusCode, redirect};
use serde::{Deserialize, Serialize};

use crate::{OriginPolicy, PortalOrigin};

/// The documented, unauthenticated-safe Portal endpoint used for the check.
pub const PROBE_PATH: &str = "/api/manifest";
pub const PROBE_TIMEOUT: Duration = Duration::from_secs(10);
const DISCOVERY_PATH: &str = "/.well-known/digital-fortress/manifest.json";
const DISCOVERY_MAX_BYTES: usize = 8 * 1024;

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
    DiscoveryInvalid,
    DiscoveryTooLarge,
    DiscoveryResponse {
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

#[derive(Deserialize)]
struct DiscoveryDocument {
    origin: String,
}

/// @spec CLIENT-023 CLIENT-024 CLIENT-025 CLIENT-026
pub async fn check_portal(
    client: &Client,
    origin: &PortalOrigin,
    policy: OriginPolicy,
) -> (PortalOrigin, ProbeOutcome) {
    match discover_origin(client, origin, policy).await {
        Ok(selected) => {
            let outcome = probe(client, &selected).await;
            (selected, outcome)
        }
        Err(outcome) => (origin.clone(), outcome),
    }
}

async fn discover_origin(
    client: &Client,
    origin: &PortalOrigin,
    policy: OriginPolicy,
) -> Result<PortalOrigin, ProbeOutcome> {
    let mut response = client
        .get(origin.join(DISCOVERY_PATH))
        .header(ACCEPT, "application/json")
        .send()
        .await
        .map_err(|error| classify_error(&error))?;

    match response.status() {
        StatusCode::NOT_FOUND | StatusCode::FOUND => return Ok(origin.clone()),
        StatusCode::OK => {}
        status => {
            return Err(ProbeOutcome::DiscoveryResponse {
                status: status.as_u16(),
            });
        }
    }

    if response
        .content_length()
        .is_some_and(|length| length > DISCOVERY_MAX_BYTES as u64)
    {
        return Err(ProbeOutcome::DiscoveryTooLarge);
    }
    let mut body = Vec::new();
    while let Some(chunk) = response
        .chunk()
        .await
        .map_err(|error| classify_error(&error))?
    {
        if chunk.len() > DISCOVERY_MAX_BYTES - body.len() {
            return Err(ProbeOutcome::DiscoveryTooLarge);
        }
        body.extend_from_slice(&chunk);
    }

    let document: DiscoveryDocument =
        serde_json::from_slice(&body).map_err(|_| ProbeOutcome::DiscoveryInvalid)?;
    if !document.origin.contains("://") {
        return Err(ProbeOutcome::DiscoveryInvalid);
    }
    PortalOrigin::parse(&document.origin, policy).map_err(|_| ProbeOutcome::DiscoveryInvalid)
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
        v.split_ascii_whitespace()
            .next()
            .is_some_and(|scheme| scheme.eq_ignore_ascii_case("bearer"))
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

    struct ScriptedServer {
        origin: String,
        requests: Arc<Mutex<Vec<String>>>,
    }

    async fn serve_scripted(responses: Vec<String>) -> ScriptedServer {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let port = listener.local_addr().unwrap().port();
        let requests = Arc::new(Mutex::new(Vec::new()));
        let recorded = requests.clone();
        tokio::spawn(async move {
            let mut responses = responses.into_iter();
            loop {
                let Ok((mut socket, _)) = listener.accept().await else {
                    return;
                };
                let mut buffer = vec![0; 8192];
                let read = socket.read(&mut buffer).await.unwrap_or(0);
                recorded
                    .lock()
                    .await
                    .push(String::from_utf8_lossy(&buffer[..read]).into_owned());
                let response = responses.next().unwrap_or_else(|| {
                    "HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\nConnection: close\r\n\r\n".into()
                });
                let _ = socket.write_all(response.as_bytes()).await;
                let _ = socket.shutdown().await;
            }
        });
        ScriptedServer {
            origin: format!("http://127.0.0.1:{port}"),
            requests,
        }
    }

    fn response(status: &str, body: &str) -> String {
        format!(
            "HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{body}",
            body.len()
        )
    }

    async fn check_discovery(server: &ScriptedServer) -> (PortalOrigin, ProbeOutcome) {
        check_portal(
            &probe_client(PROBE_TIMEOUT).unwrap(),
            &origin(&server.origin),
            OriginPolicy::AllowHttpLoopback,
        )
        .await
    }

    // @spec CLIENT-023 CLIENT-024 CLIENT-026
    #[tokio::test]
    async fn discovery_checks_and_selects_the_document_origin_without_recursion() {
        let target = serve_scripted(vec![PORTAL_401.into()]).await;
        let document = serde_json::json!({"origin": target.origin, "extra": "ignored"});
        let alias = serve_scripted(vec![response("200 OK", &document.to_string())]).await;

        let (selected, outcome) = check_discovery(&alias).await;

        assert_eq!(selected.to_string(), target.origin);
        assert_eq!(outcome, ProbeOutcome::SignInRequired);
        let alias_requests = alias.requests.lock().await;
        let target_requests = target.requests.lock().await;
        assert_eq!(alias_requests.len(), 1);
        assert!(alias_requests[0].starts_with("GET /.well-known/digital-fortress/manifest.json "));
        assert_eq!(target_requests.len(), 1);
        assert!(target_requests[0].starts_with("GET /api/manifest "));
        for request in [alias_requests[0].as_str(), target_requests[0].as_str()] {
            let request = request.to_ascii_lowercase();
            assert!(!request.contains("\r\nauthorization:"));
            assert!(!request.contains("\r\ncookie:"));
        }
    }

    // @spec CLIENT-023 CLIENT-025
    #[tokio::test]
    async fn missing_discovery_document_falls_back_to_entered_origin() {
        let server = serve_scripted(vec![response("404 Not Found", ""), PORTAL_401.into()]).await;

        let (selected, outcome) = check_discovery(&server).await;

        assert_eq!(selected.to_string(), server.origin);
        assert_eq!(outcome, ProbeOutcome::SignInRequired);
        let requests = server.requests.lock().await;
        assert_eq!(requests.len(), 2);
        assert!(requests[0].starts_with("GET /.well-known/digital-fortress/manifest.json "));
        assert!(requests[1].starts_with("GET /api/manifest "));
    }

    // @spec CLIENT-023 CLIENT-025
    #[tokio::test]
    async fn discovery_302_falls_back_without_following_the_redirect() {
        let server = serve_scripted(vec![
            "HTTP/1.1 302 Found\r\nLocation: https://other.example/login\r\nContent-Length: 0\r\nConnection: close\r\n\r\n".into(),
            PORTAL_401.into(),
        ])
        .await;

        let (selected, outcome) = check_discovery(&server).await;

        assert_eq!(selected.to_string(), server.origin);
        assert_eq!(outcome, ProbeOutcome::SignInRequired);
        let requests = server.requests.lock().await;
        assert_eq!(requests.len(), 2);
        assert!(requests[0].starts_with("GET /.well-known/digital-fortress/manifest.json "));
        assert!(requests[1].starts_with("GET /api/manifest "));
    }

    // @spec CLIENT-024 CLIENT-025
    #[tokio::test]
    async fn discovery_rejects_bad_documents_without_fallback() {
        for body in [
            "not json",
            "{}",
            r#"{"origin":42}"#,
            r#"{"origin":"example.com"}"#,
            r#"{"origin":"https://example.com/path"}"#,
            r#"{"origin":"http://example.com"}"#,
        ] {
            let server = serve_scripted(vec![response("200 OK", body)]).await;
            let (_, outcome) = check_discovery(&server).await;
            assert_eq!(outcome, ProbeOutcome::DiscoveryInvalid, "{body}");
            assert_eq!(server.requests.lock().await.len(), 1);
        }
    }

    // @spec CLIENT-024
    #[tokio::test]
    async fn discovery_enforces_the_exact_body_size_limit() {
        let prefix = r#"{"origin":"http://example.com","padding":""#;
        let suffix = r#""}"#;
        for (size, expected) in [
            (8192, ProbeOutcome::DiscoveryInvalid),
            (8193, ProbeOutcome::DiscoveryTooLarge),
        ] {
            let body = format!(
                "{prefix}{}{suffix}",
                "x".repeat(size - prefix.len() - suffix.len())
            );
            let server = serve_scripted(vec![response("200 OK", &body)]).await;
            let (_, outcome) = check_discovery(&server).await;
            assert_eq!(outcome, expected, "body size {size}");
            assert_eq!(server.requests.lock().await.len(), 1);
        }
    }

    // @spec CLIENT-024
    #[tokio::test]
    async fn discovery_limits_a_chunked_response_without_a_content_length() {
        let body = format!(
            "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n2000\r\n{}\r\n1\r\nx\r\n0\r\n\r\n",
            "x".repeat(8192)
        );
        let server = serve_scripted(vec![body]).await;

        let (_, outcome) = check_discovery(&server).await;

        assert_eq!(outcome, ProbeOutcome::DiscoveryTooLarge);
        assert_eq!(server.requests.lock().await.len(), 1);
    }

    // @spec CLIENT-023 CLIENT-025
    #[tokio::test]
    async fn discovery_timeout_does_not_fall_back() {
        let server = serve(None).await;
        let client = probe_client(Duration::from_millis(100)).unwrap();

        let (_, outcome) = check_portal(
            &client,
            &origin(&server.origin),
            OriginPolicy::AllowHttpLoopback,
        )
        .await;

        assert_eq!(outcome, ProbeOutcome::Timeout);
        assert_eq!(server.requests.load(Ordering::SeqCst), 1);
    }

    // @spec CLIENT-023 CLIENT-025
    #[tokio::test]
    async fn discovery_redirects_and_other_statuses_do_not_fall_back() {
        for (response, status) in [
            ("HTTP/1.1 301 Moved Permanently\r\nLocation: /login\r\nContent-Length: 0\r\nConnection: close\r\n\r\n".to_string(), 301),
            (response("403 Forbidden", ""), 403),
            (response("500 Internal Server Error", ""), 500),
        ] {
            let server = serve_scripted(vec![response]).await;
            let (_, outcome) = check_discovery(&server).await;
            assert_eq!(outcome, ProbeOutcome::DiscoveryResponse { status });
            assert_eq!(server.requests.lock().await.len(), 1);
        }
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
