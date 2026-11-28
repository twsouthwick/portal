use std::net::{Ipv4Addr, SocketAddr};

use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio::net::{TcpListener, TcpStream};
use url::Url;

use crate::oidc::SignInError;

pub const LOOPBACK_PORT: u16 = 49152;
const CALLBACK_PATH: &str = "/oidc-callback";
const MAX_REQUEST_BYTES: usize = 16 * 1024;

const DONE_PAGE: &str = "<!doctype html><html><head><meta charset=\"utf-8\"><title>Digital Fortress</title></head>\
<body style=\"font-family:sans-serif;background:#0b1020;color:#e8ecff;display:grid;place-items:center;height:100vh;margin:0\">\
<p>Sign-in finished. You can close this window and return to Digital Fortress.</p></body></html>";

/// The sign-in callback listener, bound only to `127.0.0.1`.
pub struct CallbackListener {
    listener: TcpListener,
}

impl CallbackListener {
    /// @spec CLIENT-030
    pub async fn bind() -> Result<Self, SignInError> {
        Self::bind_port(LOOPBACK_PORT).await
    }

    pub async fn bind_port(port: u16) -> Result<Self, SignInError> {
        let listener = TcpListener::bind(SocketAddr::from((Ipv4Addr::LOCALHOST, port)))
            .await
            .map_err(|_| SignInError::PortUnavailable)?;
        Ok(Self { listener })
    }

    pub fn port(&self) -> u16 {
        self.listener.local_addr().map(|addr| addr.port()).unwrap_or_default()
    }

    /// Waits for `GET /oidc-callback`, answering other requests with an error, and
    /// returns the callback as a URL on `127.0.0.1`. The listener closes when
    /// this returns or the future is dropped.
    /// @spec CLIENT-030
    pub async fn accept_callback(self) -> Url {
        loop {
            let Ok((mut socket, _)) = self.listener.accept().await else {
                continue;
            };
            if let Some(target) = read_callback_target(&mut socket).await {
                respond(&mut socket, "200 OK", DONE_PAGE).await;
                let url = Url::parse(&format!("http://127.0.0.1:{}{target}", self.port()));
                if let Ok(url) = url {
                    return url;
                }
            } else {
                respond(&mut socket, "404 Not Found", "Not found").await;
            }
        }
    }
}

async fn read_callback_target(socket: &mut TcpStream) -> Option<String> {
    let mut buffer = Vec::new();
    let mut chunk = [0u8; 2048];
    while !buffer.windows(4).any(|w| w == b"\r\n\r\n") {
        let read = socket.read(&mut chunk).await.ok()?;
        if read == 0 || buffer.len() + read > MAX_REQUEST_BYTES {
            return None;
        }
        buffer.extend_from_slice(&chunk[..read]);
    }
    let head = std::str::from_utf8(&buffer).ok()?;
    let mut parts = head.lines().next()?.split(' ');
    let (method, target) = (parts.next()?, parts.next()?);
    let path = target.split('?').next()?;
    (method == "GET" && path == CALLBACK_PATH).then(|| target.to_owned())
}

async fn respond(socket: &mut TcpStream, status: &str, body: &str) {
    let response = format!(
        "HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {}\r\n\
Cache-Control: no-store\r\nConnection: close\r\n\r\n{body}",
        body.len()
    );
    let _ = socket.write_all(response.as_bytes()).await;
    let _ = socket.shutdown().await;
}

#[cfg(test)]
mod tests {
    use super::*;

    // @spec CLIENT-030
    #[tokio::test]
    async fn accepts_only_get_callback_on_loopback() {
        let listener = CallbackListener::bind_port(0).await.unwrap();
        let port = listener.port();
        let task = tokio::spawn(listener.accept_callback());
        let http = reqwest::Client::new();
        let base = format!("http://127.0.0.1:{port}");

        assert_eq!(http.get(format!("{base}/other")).send().await.unwrap().status(), 404);
        assert_eq!(http.get(format!("{base}/callback")).send().await.unwrap().status(), 404);
        assert_eq!(
            http.post(format!("{base}/oidc-callback?code=x&state=y")).send().await.unwrap().status(),
            404
        );
        let ok = http.get(format!("{base}/oidc-callback?code=x&state=y")).send().await.unwrap();
        assert_eq!(ok.status(), 200);
        let url = task.await.unwrap();
        assert_eq!(url.as_str(), format!("{base}/oidc-callback?code=x&state=y"));
        // The listener is closed after the callback.
        assert!(http.get(format!("{base}/oidc-callback")).send().await.is_err());
    }

    // @spec CLIENT-030
    #[tokio::test]
    async fn reports_an_unavailable_port() {
        let held = CallbackListener::bind_port(0).await.unwrap();
        assert_eq!(
            CallbackListener::bind_port(held.port()).await.err(),
            Some(SignInError::PortUnavailable)
        );
    }
}
