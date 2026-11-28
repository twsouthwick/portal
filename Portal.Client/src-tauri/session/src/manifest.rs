use reqwest::header::{ACCEPT, AUTHORIZATION, ETAG, IF_NONE_MATCH};
use reqwest::{Client, StatusCode};
use serde::{Deserialize, Serialize};

use portal_onboarding::PortalOrigin;

pub const MANIFEST_PATH: &str = "/api/manifest";
pub const SUPPORTED_SCHEMA_VERSION: u32 = 1;
const MANIFEST_MAX_BYTES: usize = 4 * 1024 * 1024;

/// The client manifest DTO (SITE-018). Unknown fields are dropped so only the
/// documented DTO is cached.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Manifest {
    pub schema_version: u32,
    pub name: String,
    pub description: String,
    pub sites: Vec<Site>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Site {
    pub id: String,
    pub name: String,
    pub description: String,
    pub url: Option<String>,
    pub icon: String,
    pub categories: Vec<String>,
    pub usage_instructions: String,
    #[serde(default)]
    pub is_internal: bool,
    #[serde(default)]
    pub is_private: bool,
    pub apps: Vec<SiteApp>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SiteApp {
    pub name: String,
    pub scheme: String,
    pub store: String,
    pub store_id: String,
}

impl Manifest {
    /// @spec CLIENT-019
    pub fn parse(body: &[u8]) -> Result<Self, String> {
        let manifest: Manifest =
            serde_json::from_slice(body).map_err(|error| format!("Invalid manifest: {error}"))?;
        if manifest.schema_version != SUPPORTED_SCHEMA_VERSION {
            return Err(format!(
                "Unsupported manifest schema version {}.",
                manifest.schema_version
            ));
        }
        Ok(manifest)
    }
}

#[derive(Debug, PartialEq, Eq)]
pub enum FetchOutcome {
    Updated { manifest: Manifest, etag: Option<String> },
    NotModified,
    Unauthorized,
    Failed(FetchFailure),
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum FetchFailure {
    /// Transport failure: DNS, connection, TLS, or timeout.
    Network,
    /// The Portal answered with a non-success status, including `503`.
    Server(u16),
    /// The body was not a complete, valid manifest.
    Invalid,
}

/// @spec CLIENT-016 CLIENT-019
pub async fn fetch_manifest(
    client: &Client,
    origin: &PortalOrigin,
    access_token: &str,
    if_none_match: Option<&str>,
) -> FetchOutcome {
    let mut request = client
        .get(origin.join(MANIFEST_PATH))
        .header(ACCEPT, "application/json")
        .header(AUTHORIZATION, format!("Bearer {access_token}"));
    if let Some(etag) = if_none_match {
        request = request.header(IF_NONE_MATCH, etag);
    }
    let response = match request.send().await {
        Ok(response) => response,
        Err(_) => return FetchOutcome::Failed(FetchFailure::Network),
    };
    match response.status() {
        StatusCode::OK => {}
        StatusCode::NOT_MODIFIED => return FetchOutcome::NotModified,
        StatusCode::UNAUTHORIZED => return FetchOutcome::Unauthorized,
        status => return FetchOutcome::Failed(FetchFailure::Server(status.as_u16())),
    }
    let etag = response
        .headers()
        .get(ETAG)
        .and_then(|value| value.to_str().ok())
        .map(str::to_owned);
    if response
        .content_length()
        .is_some_and(|length| length > MANIFEST_MAX_BYTES as u64)
    {
        return FetchOutcome::Failed(FetchFailure::Invalid);
    }
    let body = match read_limited(response).await {
        Ok(body) => body,
        Err(failure) => return FetchOutcome::Failed(failure),
    };
    match Manifest::parse(&body) {
        Ok(manifest) => FetchOutcome::Updated { manifest, etag },
        Err(_) => FetchOutcome::Failed(FetchFailure::Invalid),
    }
}

async fn read_limited(mut response: reqwest::Response) -> Result<Vec<u8>, FetchFailure> {
    let mut body = Vec::new();
    while let Some(chunk) = response.chunk().await.map_err(|_| FetchFailure::Network)? {
        if body.len() + chunk.len() > MANIFEST_MAX_BYTES {
            return Err(FetchFailure::Invalid);
        }
        body.extend_from_slice(&chunk);
    }
    Ok(body)
}

#[cfg(test)]
mod tests {
    use super::*;

    pub(crate) const SAMPLE: &str = r#"{
        "schemaVersion": 1,
        "name": "Home",
        "description": "Family services",
        "extra": "ignored",
        "sites": [{
            "id": "media", "name": "Media", "description": "Movies",
            "url": "https://media.example.com", "icon": "bi bi-film",
            "categories": ["Entertainment"], "usageInstructions": "Use **it**",
            "isInternal": true, "isPrivate": false,
            "apps": [{"name": "Jellyfin", "scheme": "jellyfin://", "store": "google", "storeId": "org.jellyfin"}]
        }]
    }"#;

    // @spec CLIENT-017 CLIENT-019
    #[test]
    fn parses_manifest_and_drops_unknown_fields() {
        let manifest = Manifest::parse(SAMPLE.as_bytes()).unwrap();
        assert_eq!(manifest.sites[0].apps[0].store, "google");
        assert!(manifest.sites[0].is_internal);
        let json = serde_json::to_string(&manifest).unwrap();
        assert!(!json.contains("extra"));
    }

    // @spec CLIENT-019
    #[test]
    fn rejects_incomplete_or_unsupported_manifests() {
        assert!(Manifest::parse(br#"{"schemaVersion":1,"name":"x","description":"y"}"#).is_err());
        assert!(
            Manifest::parse(br#"{"schemaVersion":2,"name":"x","description":"y","sites":[]}"#)
                .is_err()
        );
        assert!(Manifest::parse(b"not json").is_err());
    }
}
