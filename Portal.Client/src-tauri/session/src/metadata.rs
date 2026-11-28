use reqwest::{Client, StatusCode};
use serde::Deserialize;
use url::{Host, Url};

use portal_onboarding::{OriginPolicy, PortalOrigin};

pub const NATIVE_OIDC_METADATA_PATH: &str = "/.well-known/portal-native-oidc";
const METADATA_MAX_BYTES: usize = 64 * 1024;

/// Portal-published native OIDC client settings.
#[derive(Clone, Debug, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct NativeOidcMetadata {
    pub issuer: String,
    pub client_id: String,
    pub api_audience: String,
    pub scopes: Vec<String>,
}

#[derive(Debug, PartialEq, Eq, thiserror::Error)]
pub enum MetadataError {
    #[error("This Digital Fortress does not offer app sign-in.")]
    NotOffered,
    #[error("Could not reach Digital Fortress to sign in.")]
    Unreachable,
    #[error("Digital Fortress returned invalid sign-in settings.")]
    Invalid,
}

/// Accepts `https`, or loopback `http` when the policy allows it.
pub(crate) fn is_secure_url(url: &Url, policy: OriginPolicy) -> bool {
    match url.scheme() {
        "https" => true,
        "http" => {
            policy == OriginPolicy::AllowHttpLoopback
                && match url.host() {
                    Some(Host::Ipv4(ip)) => ip.is_loopback(),
                    Some(Host::Ipv6(ip)) => ip.is_loopback(),
                    Some(Host::Domain(domain)) => domain.eq_ignore_ascii_case("localhost"),
                    None => false,
                }
        }
        _ => false,
    }
}

impl NativeOidcMetadata {
    fn validate(self, policy: OriginPolicy) -> Result<Self, MetadataError> {
        let issuer = Url::parse(&self.issuer).map_err(|_| MetadataError::Invalid)?;
        let valid = is_secure_url(&issuer, policy)
            && issuer.username().is_empty()
            && issuer.password().is_none()
            && issuer.query().is_none()
            && issuer.fragment().is_none()
            && !self.client_id.trim().is_empty()
            && !self.api_audience.trim().is_empty()
            && self.scopes.iter().any(|scope| scope == "openid")
            && self.scopes.iter().all(|scope| {
                !scope.is_empty() && scope.chars().all(|c| c.is_ascii_graphic() && c != '"' && c != '\\')
            });
        valid.then_some(self).ok_or(MetadataError::Invalid)
    }
}

/// @spec CLIENT-014
pub async fn fetch_native_metadata(
    client: &Client,
    origin: &PortalOrigin,
    policy: OriginPolicy,
) -> Result<NativeOidcMetadata, MetadataError> {
    let response = client
        .get(origin.join(NATIVE_OIDC_METADATA_PATH))
        .header(reqwest::header::ACCEPT, "application/json")
        .send()
        .await
        .map_err(|_| MetadataError::Unreachable)?;
    match response.status() {
        StatusCode::OK => {}
        StatusCode::NOT_FOUND => return Err(MetadataError::NotOffered),
        status if status.is_server_error() => return Err(MetadataError::Unreachable),
        _ => return Err(MetadataError::Invalid),
    }
    if response
        .content_length()
        .is_some_and(|length| length > METADATA_MAX_BYTES as u64)
    {
        return Err(MetadataError::Invalid);
    }
    let body = read_limited(response).await?;
    serde_json::from_slice::<NativeOidcMetadata>(&body)
        .map_err(|_| MetadataError::Invalid)?
        .validate(policy)
}

async fn read_limited(mut response: reqwest::Response) -> Result<Vec<u8>, MetadataError> {
    let mut body = Vec::new();
    while let Some(chunk) = response
        .chunk()
        .await
        .map_err(|_| MetadataError::Unreachable)?
    {
        if body.len().saturating_add(chunk.len()) > METADATA_MAX_BYTES {
            return Err(MetadataError::Invalid);
        }
        body.extend_from_slice(&chunk);
    }
    Ok(body)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::test_server::{TestServer, json};
    use tokio::io::{AsyncReadExt, AsyncWriteExt};
    use tokio::net::TcpListener;

    fn metadata(issuer: &str, scopes: &[&str]) -> NativeOidcMetadata {
        NativeOidcMetadata {
            issuer: issuer.into(),
            client_id: "portal-native".into(),
            api_audience: "portal-api".into(),
            scopes: scopes.iter().map(|s| s.to_string()).collect(),
        }
    }

    // @spec CLIENT-014
    #[test]
    fn validates_required_fields_and_secure_issuer() {
        let ok = metadata("https://id.example.com", &["openid", "groups"]);
        assert!(ok.clone().validate(OriginPolicy::HttpsOnly).is_ok());
        assert!(metadata("http://id.example.com", &["openid"]).validate(OriginPolicy::AllowHttpLoopback).is_err());
        assert!(metadata("http://127.0.0.1:9", &["openid"]).validate(OriginPolicy::HttpsOnly).is_err());
        assert!(metadata("http://127.0.0.1:9", &["openid"]).validate(OriginPolicy::AllowHttpLoopback).is_ok());
        assert!(metadata("https://id.example.com", &["groups"]).validate(OriginPolicy::HttpsOnly).is_err());
        let mut blank = ok.clone();
        blank.client_id = " ".into();
        assert!(blank.validate(OriginPolicy::HttpsOnly).is_err());
        let mut no_audience = ok;
        no_audience.api_audience = String::new();
        assert!(no_audience.validate(OriginPolicy::HttpsOnly).is_err());
    }

    // @spec CLIENT-014
    #[tokio::test]
    async fn fetches_metadata_without_following_redirects() {
        let server = TestServer::start(|request| match request.path.as_str() {
            NATIVE_OIDC_METADATA_PATH => json(
                200,
                r#"{"issuer":"http://127.0.0.1:1","clientId":"portal-native","apiAudience":"portal-api","scopes":["openid","offline_access"],"extra":1}"#,
            ),
            _ => (404, vec![], String::new()),
        })
        .await;
        let client = crate::http_client().unwrap();
        let found = fetch_native_metadata(&client, &server.origin(), OriginPolicy::AllowHttpLoopback)
            .await
            .unwrap();
        assert_eq!(found.client_id, "portal-native");

        let redirect = TestServer::start(|_| {
            (302, vec![("Location".into(), "https://evil.example.com/".into())], String::new())
        })
        .await;
        assert_eq!(
            fetch_native_metadata(&client, &redirect.origin(), OriginPolicy::AllowHttpLoopback).await,
            Err(MetadataError::Invalid)
        );
        let missing = TestServer::start(|_| (404, vec![], String::new())).await;
        assert_eq!(
            fetch_native_metadata(&client, &missing.origin(), OriginPolicy::AllowHttpLoopback).await,
            Err(MetadataError::NotOffered)
        );
    }

    // @spec CLIENT-014
    #[tokio::test]
    async fn rejects_chunked_metadata_as_soon_as_it_exceeds_limit() {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let address = listener.local_addr().unwrap();
        let oversized = vec![b'a'; METADATA_MAX_BYTES + 1];
        let server = tokio::spawn(async move {
            let (mut socket, _) = listener.accept().await.unwrap();
            let mut request = [0; 1024];
            let _ = socket.read(&mut request).await;
            socket
                .write_all(
                    format!(
                        "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nConnection: keep-alive\r\n\r\n{:X}\r\n",
                        oversized.len()
                    )
                    .as_bytes(),
                )
                .await
                .unwrap();
            socket.write_all(&oversized).await.unwrap();
            socket.write_all(b"\r\n").await.unwrap();
            let mut byte = [0];
            socket.read(&mut byte).await.unwrap()
        });
        let client = Client::builder()
            .timeout(std::time::Duration::from_secs(2))
            .build()
            .unwrap();
        let origin = PortalOrigin::parse(
            &format!("http://{address}"),
            OriginPolicy::AllowHttpLoopback,
        )
        .unwrap();

        assert_eq!(
            fetch_native_metadata(&client, &origin, OriginPolicy::AllowHttpLoopback).await,
            Err(MetadataError::Invalid)
        );
        assert_eq!(
            server.await.unwrap(),
            0,
            "client should stop reading the response"
        );
    }
}
