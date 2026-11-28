use std::path::PathBuf;
use std::sync::Arc;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

use reqwest::Client;
use serde::Serialize;
use tokio::sync::Mutex;
use url::Url;

use portal_onboarding::{OriginPolicy, PortalOrigin};

use crate::cache::{CachedManifest, delete_cache, read_cache, write_cache};
use crate::credentials::{Account, CacheKey, CredentialStore, StoredSession};
use crate::manifest::{FetchFailure, FetchOutcome, Manifest, fetch_manifest};
use crate::metadata::{MetadataError, fetch_native_metadata};
use crate::oidc::{OidcProvider, PendingSignIn, SignInError};

const TOKEN_EXPIRY_MARGIN: Duration = Duration::from_secs(30);
pub const NOT_PERSISTED_WARNING: &str =
    "Secure storage is unavailable on this device, so you will need to sign in again next time.";

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum SessionStatus {
    SignedOut,
    Refreshing,
    UpToDate,
    Offline,
    Failed,
    SignInRequired,
}

/// What the UI shows for the saved Portal.
#[derive(Clone, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SessionView {
    pub status: SessionStatus,
    pub manifest: Option<Manifest>,
    /// Unix seconds of the last successful refresh.
    pub refreshed_at: Option<u64>,
    pub warning: Option<String>,
}

struct Active {
    stored: StoredSession,
    access_token: Option<String>,
    expires_at: Option<Instant>,
    persisted: bool,
}

impl Active {
    fn warning(&self) -> Option<String> {
        (!self.persisted).then(|| NOT_PERSISTED_WARNING.to_owned())
    }
}

#[derive(Default)]
struct State {
    loaded: bool,
    active: Option<Active>,
    provider: Option<(String, OidcProvider)>,
    pending: Option<PendingSignIn>,
}

/// Owns the signed-in account, its tokens, and its encrypted manifest cache.
pub struct SessionManager {
    http: Client,
    policy: OriginPolicy,
    data_dir: PathBuf,
    store: Arc<dyn CredentialStore>,
    state: Mutex<State>,
}

fn now_seconds() -> u64 {
    SystemTime::now().duration_since(UNIX_EPOCH).map(|d| d.as_secs()).unwrap_or_default()
}

impl SessionManager {
    pub fn new(
        http: Client,
        policy: OriginPolicy,
        data_dir: PathBuf,
        store: Arc<dyn CredentialStore>,
    ) -> Self {
        Self { http, policy, data_dir, store, state: Mutex::new(State::default()) }
    }

    fn signed_out(warning: Option<String>) -> SessionView {
        SessionView { status: SessionStatus::SignedOut, manifest: None, refreshed_at: None, warning }
    }

    fn view(&self, active: &Active, status: SessionStatus, cache: Option<CachedManifest>) -> SessionView {
        SessionView {
            status,
            refreshed_at: cache.as_ref().map(|c| c.refreshed_at),
            manifest: cache.map(|c| c.manifest),
            warning: active.warning(),
        }
    }

    fn read(&self, active: &Active) -> Option<CachedManifest> {
        read_cache(&self.data_dir, &active.stored.cache_key, &active.stored.account)
    }

    fn load(&self, state: &mut State) {
        if state.loaded {
            return;
        }
        state.loaded = true;
        if let Ok(Some(stored)) = self.store.load() {
            state.active = Some(Active { stored, access_token: None, expires_at: None, persisted: true });
        }
    }

    /// Shows the cached manifest for the saved account, if any, before refresh.
    /// @spec CLIENT-018 CLIENT-035
    pub async fn startup(&self, origin: &PortalOrigin) -> SessionView {
        let mut state = self.state.lock().await;
        self.load(&mut state);
        match state.active.as_ref() {
            Some(active) if active.stored.account.origin == origin.as_str() => {
                self.view(active, SessionStatus::Refreshing, self.read(active))
            }
            _ => Self::signed_out(None),
        }
    }

    async fn provider(&self, state: &mut State, origin: &PortalOrigin) -> Result<OidcProvider, SignInError> {
        if let Some((cached_origin, provider)) = &state.provider
            && cached_origin == origin.as_str()
        {
            return Ok(provider.clone());
        }
        let native = fetch_native_metadata(&self.http, origin, self.policy).await?;
        let provider = OidcProvider::discover(&self.http, native, self.policy).await?;
        state.provider = Some((origin.as_str().to_owned(), provider.clone()));
        Ok(provider)
    }

    /// Returns a usable access token, refreshing it when it is missing or
    /// expiring. `Ok(None)` means sign-in is required.
    async fn access_token(
        &self,
        state: &mut State,
        origin: &PortalOrigin,
        force_refresh: bool,
    ) -> Result<Option<String>, SignInError> {
        let active = state.active.as_ref().expect("active session");
        let fresh = active
            .expires_at
            .is_none_or(|expires| expires > Instant::now() + TOKEN_EXPIRY_MARGIN);
        if !force_refresh && fresh && let Some(token) = &active.access_token {
            return Ok(Some(token.clone()));
        }
        let Some(refresh_token) = active.stored.refresh_token.clone() else {
            return Ok(None);
        };
        let provider = self.provider(state, origin).await?;
        let tokens = match provider.refresh(&self.http, &refresh_token).await {
            Ok(tokens) => tokens,
            Err(SignInError::Token) => {
                if let Some(active) = state.active.as_mut() {
                    active.access_token = None;
                }
                return Ok(None);
            }
            Err(error) => return Err(error),
        };
        let active = state.active.as_mut().expect("active session");
        active.access_token = Some(tokens.access_token.clone());
        active.expires_at = tokens.expires_at;
        if let Some(rotated) = tokens.refresh_token
            && Some(&rotated) != active.stored.refresh_token.as_ref()
        {
            active.stored.refresh_token = Some(rotated);
            if active.persisted && self.store.save(&active.stored).is_err() {
                active.persisted = false;
            }
        }
        Ok(Some(tokens.access_token))
    }

    /// @spec CLIENT-016 CLIENT-018 CLIENT-019 CLIENT-020
    pub async fn refresh(&self, origin: &PortalOrigin) -> SessionView {
        let mut state = self.state.lock().await;
        self.load(&mut state);
        let Some(active) = state.active.as_ref() else {
            return Self::signed_out(None);
        };
        if active.stored.account.origin != origin.as_str() {
            return Self::signed_out(None);
        }
        let mut cache = self.read(active);
        let mut retried_unauthorized = false;
        let mut retried_not_modified = false;
        loop {
            let token = match self.access_token(&mut state, origin, retried_unauthorized).await {
                Ok(Some(token)) => token,
                Ok(None) => {
                    let active = state.active.as_ref().expect("active session");
                    return self.view(active, SessionStatus::SignInRequired, cache);
                }
                Err(error) => {
                    let status = match error {
                        SignInError::Metadata(MetadataError::Unreachable)
                        | SignInError::Discovery
                        | SignInError::Unreachable => SessionStatus::Offline,
                        _ => SessionStatus::Failed,
                    };
                    let active = state.active.as_ref().expect("active session");
                    return self.view(active, status, cache);
                }
            };
            let etag = if retried_not_modified {
                None
            } else {
                cache.as_ref().and_then(|c| c.etag.clone())
            };
            let outcome = fetch_manifest(&self.http, origin, &token, etag.as_deref()).await;
            let active = state.active.as_ref().expect("active session");
            match outcome {
                FetchOutcome::Updated { manifest, etag } => {
                    let updated = CachedManifest {
                        account: active.stored.account.clone(),
                        etag,
                        refreshed_at: now_seconds(),
                        manifest,
                    };
                    return self.store_and_view(active, updated);
                }
                FetchOutcome::NotModified => match cache.take() {
                    Some(mut existing) if etag.is_some() => {
                        existing.refreshed_at = now_seconds();
                        return self.store_and_view(active, existing);
                    }
                    other if !retried_not_modified => {
                        cache = other;
                        retried_not_modified = true;
                    }
                    other => return self.view(active, SessionStatus::Failed, other),
                },
                FetchOutcome::Unauthorized if !retried_unauthorized => retried_unauthorized = true,
                FetchOutcome::Unauthorized => {
                    let active = state.active.as_mut().expect("active session");
                    active.access_token = None;
                    let active = state.active.as_ref().expect("active session");
                    return self.view(active, SessionStatus::SignInRequired, cache);
                }
                FetchOutcome::Failed(FetchFailure::Network) => {
                    return self.view(active, SessionStatus::Offline, cache);
                }
                FetchOutcome::Failed(_) => return self.view(active, SessionStatus::Failed, cache),
            }
        }
    }

    fn store_and_view(&self, active: &Active, cached: CachedManifest) -> SessionView {
        let mut view = self.view(active, SessionStatus::UpToDate, Some(cached.clone()));
        // Without a persisted cache key the ciphertext could never be read again.
        if active.persisted
            && let Err(error) = write_cache(&self.data_dir, &active.stored.cache_key, &cached)
        {
            view.warning = Some(error.to_string());
        }
        view
    }

    /// Starts the single sign-in transaction and returns the URL to open in
    /// the system browser.
    /// @spec CLIENT-014 CLIENT-032
    pub async fn begin_sign_in(&self, origin: &PortalOrigin, redirect_uri: &str) -> Result<Url, SignInError> {
        let mut state = self.state.lock().await;
        state.pending = None;
        // Re-fetch metadata for each sign-in.
        state.provider = None;
        let provider = self.provider(&mut state, origin).await?;
        let (pending, url) = provider.begin(redirect_uri)?;
        state.pending = Some(pending);
        Ok(url)
    }

    /// @spec CLIENT-032
    pub async fn cancel_sign_in(&self) {
        self.state.lock().await.pending = None;
    }

    /// Completes sign-in from the callback, persists the session, and loads
    /// the manifest.
    /// @spec CLIENT-015 CLIENT-033 CLIENT-034
    pub async fn complete_sign_in(&self, origin: &PortalOrigin, callback: &Url) -> Result<SessionView, SignInError> {
        {
            let mut state = self.state.lock().await;
            self.load(&mut state);
            let pending = state.pending.take().ok_or(SignInError::Cancelled)?;
            let provider = match &state.provider {
                Some((cached_origin, provider)) if cached_origin == origin.as_str() => provider.clone(),
                _ => return Err(SignInError::Cancelled),
            };
            let tokens = provider.complete(&self.http, pending, callback).await?;
            let account = Account {
                origin: origin.as_str().to_owned(),
                issuer: tokens.issuer,
                subject: tokens.subject.ok_or(SignInError::InvalidIdToken)?,
            };
            let cache_key = match state.active.take() {
                Some(previous) if previous.stored.account == account => previous.stored.cache_key,
                Some(previous) => {
                    let _ = delete_cache(&self.data_dir, &previous.stored.account);
                    CacheKey::generate().map_err(|_| SignInError::Token)?
                }
                None => CacheKey::generate().map_err(|_| SignInError::Token)?,
            };
            let stored = StoredSession { account, refresh_token: tokens.refresh_token, cache_key };
            let persisted = self.store.save(&stored).is_ok();
            state.active = Some(Active {
                stored,
                access_token: Some(tokens.access_token),
                expires_at: tokens.expires_at,
                persisted,
            });
        }
        Ok(self.refresh(origin).await)
    }

    /// Clears the account's cache and credentials.
    /// @spec CLIENT-021
    pub async fn sign_out(&self) -> SessionView {
        let mut state = self.state.lock().await;
        self.load(&mut state);
        state.pending = None;
        let mut warning = None;
        if let Some(active) = state.active.take()
            && let Err(error) = delete_cache(&self.data_dir, &active.stored.account)
        {
            warning = Some(error.to_string());
        }
        if self.store.delete().is_err() {
            warning = Some("Could not remove saved sign-in from secure storage.".into());
        }
        Self::signed_out(warning)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::credentials::MemoryCredentialStore;
    use crate::oidc::DESKTOP_REDIRECT_URI;
    use crate::oidc::tests::{MockIdp, authorize, mock_idp};
    use crate::test_server::{TestServer, json};
    use std::sync::Mutex as StdMutex;

    const MANIFEST: &str = r#"{"schemaVersion":1,"name":"Home","description":"Family","sites":[]}"#;

    /// (status, etag) per manifest request.
    type Script = Vec<(u16, Option<&'static str>)>;

    /// Portal script per manifest request; metadata always served.
    struct Portal {
        server: TestServer,
        script: Arc<StdMutex<Script>>,
    }

    async fn portal(idp: &MockIdp, script: Script) -> Portal {
        let script = Arc::new(StdMutex::new(script));
        let native = serde_json::json!({
            "issuer": idp.issuer(), "clientId": "portal-native",
            "apiAudience": "portal-api", "scopes": ["openid", "offline_access"]
        })
        .to_string();
        let s = script.clone();
        let server = TestServer::start(move |request| match request.path.as_str() {
            "/.well-known/portal-native-oidc" => json(200, native.clone()),
            "/api/manifest" => {
                let mut script = s.lock().unwrap();
                let (status, etag) = if script.is_empty() { (200, Some("\"v1\"")) } else { script.remove(0) };
                let headers = etag.map(|e| vec![("ETag".to_string(), e.to_string())]).unwrap_or_default();
                match status {
                    200 => (200, headers, MANIFEST.into()),
                    status => (status, headers, String::new()),
                }
            }
            _ => (404, vec![], String::new()),
        })
        .await;
        Portal { server, script }
    }

    impl Portal {
        fn manifest_requests(&self) -> Vec<crate::test_server::Request> {
            self.server.requests().into_iter().filter(|r| r.path == "/api/manifest").collect()
        }
    }

    fn manager(dir: &std::path::Path, store: Arc<dyn CredentialStore>) -> SessionManager {
        SessionManager::new(
            crate::http_client().unwrap(),
            OriginPolicy::AllowHttpLoopback,
            dir.to_path_buf(),
            store,
        )
    }

    async fn sign_in(manager: &SessionManager, origin: &PortalOrigin) -> SessionView {
        let url = manager.begin_sign_in(origin, DESKTOP_REDIRECT_URI).await.unwrap();
        let callback = authorize(&crate::http_client().unwrap(), &url, DESKTOP_REDIRECT_URI).await;
        manager.complete_sign_in(origin, &callback).await.unwrap()
    }

    // @spec CLIENT-016 CLIENT-034 CLIENT-035
    #[tokio::test]
    async fn signs_in_fetches_manifest_and_persists_session() {
        let idp = mock_idp().await;
        let portal = portal(&idp, vec![]).await;
        let dir = tempfile::tempdir().unwrap();
        let store = Arc::new(MemoryCredentialStore::default());
        let manager = manager(dir.path(), store.clone());

        let view = sign_in(&manager, &portal.server.origin()).await;
        assert_eq!(view.status, SessionStatus::UpToDate);
        assert_eq!(view.manifest.unwrap().name, "Home");
        assert_eq!(view.warning, None);
        let request = &portal.manifest_requests()[0];
        assert_eq!(request.method, "GET");
        assert_eq!(request.header("authorization"), Some("Bearer access-1"));
        assert_eq!(request.header("if-none-match"), None);

        let stored = store.load().unwrap().unwrap();
        assert_eq!(stored.account.subject, "alice");
        assert_eq!(stored.account.issuer, idp.issuer());
        assert_eq!(stored.account.origin, portal.server.origin().as_str());
        assert_eq!(stored.refresh_token.as_deref(), Some("refresh-1"));
    }

    // @spec CLIENT-018 CLIENT-019
    #[tokio::test]
    async fn restart_shows_cache_then_revalidates_with_etag() {
        let idp = mock_idp().await;
        let portal = portal(&idp, vec![(200, Some("\"v1\"")), (304, None)]).await;
        let dir = tempfile::tempdir().unwrap();
        let store: Arc<dyn CredentialStore> = Arc::new(MemoryCredentialStore::default());
        let origin = portal.server.origin();
        sign_in(&manager(dir.path(), store.clone()), &origin).await;

        let restarted = manager(dir.path(), store);
        let startup = restarted.startup(&origin).await;
        assert_eq!(startup.status, SessionStatus::Refreshing);
        assert_eq!(startup.manifest.as_ref().unwrap().name, "Home");

        let view = restarted.refresh(&origin).await;
        assert_eq!(view.status, SessionStatus::UpToDate);
        assert_eq!(view.manifest.unwrap().name, "Home");
        let requests = portal.manifest_requests();
        assert_eq!(requests[1].header("if-none-match"), Some("\"v1\""));
        // Restart has no access token, so it refreshed with the stored refresh token.
        assert_eq!(requests[1].header("authorization"), Some("Bearer access-2"));
    }

    // @spec CLIENT-020
    #[tokio::test]
    async fn retries_once_after_401_then_requires_sign_in() {
        let idp = mock_idp().await;
        let portal = portal(&idp, vec![(200, Some("\"v1\"")), (401, None), (200, Some("\"v2\""))]).await;
        let dir = tempfile::tempdir().unwrap();
        let manager = manager(dir.path(), Arc::new(MemoryCredentialStore::default()));
        let origin = portal.server.origin();
        sign_in(&manager, &origin).await;

        assert_eq!(manager.refresh(&origin).await.status, SessionStatus::UpToDate);
        assert_eq!(portal.manifest_requests()[2].header("authorization"), Some("Bearer access-2"));

        portal.script.lock().unwrap().extend([(401, None), (401, None)]);
        let view = manager.refresh(&origin).await;
        assert_eq!(view.status, SessionStatus::SignInRequired);
        assert_eq!(view.manifest.unwrap().name, "Home", "stale manifest stays visible");
        assert_eq!(portal.manifest_requests().len(), 5);

        idp.behavior.lock().unwrap().refresh_fails = true;
        let view = manager.refresh(&origin).await;
        assert_eq!(view.status, SessionStatus::SignInRequired);
        assert_eq!(portal.manifest_requests().len(), 5, "no request without a token");
    }

    // @spec CLIENT-018
    #[tokio::test]
    async fn keeps_stale_manifest_when_offline_or_failed() {
        let idp = mock_idp().await;
        let portal = portal(&idp, vec![(200, Some("\"v1\"")), (503, None)]).await;
        let dir = tempfile::tempdir().unwrap();
        let manager = manager(dir.path(), Arc::new(MemoryCredentialStore::default()));
        let origin = portal.server.origin();
        let signed_in = sign_in(&manager, &origin).await;

        let failed = manager.refresh(&origin).await;
        assert_eq!(failed.status, SessionStatus::Failed);
        assert_eq!(failed.refreshed_at, signed_in.refreshed_at);
        assert!(failed.manifest.is_some());

        drop(portal);
        let offline_origin = PortalOrigin::parse("http://127.0.0.1:9", OriginPolicy::AllowHttpLoopback).unwrap();
        // A different origin never sees this account's cache.
        assert_eq!(manager.refresh(&offline_origin).await.status, SessionStatus::SignedOut);
    }

    // @spec CLIENT-021 CLIENT-035
    #[tokio::test]
    async fn sign_out_clears_cache_and_credentials() {
        let idp = mock_idp().await;
        let portal = portal(&idp, vec![]).await;
        let dir = tempfile::tempdir().unwrap();
        let store = Arc::new(MemoryCredentialStore::default());
        let manager = manager(dir.path(), store.clone());
        let origin = portal.server.origin();
        sign_in(&manager, &origin).await;
        let account = store.load().unwrap().unwrap().account;

        assert_eq!(manager.sign_out().await.status, SessionStatus::SignedOut);
        assert!(store.load().unwrap().is_none());
        assert!(!crate::cache::cache_path(dir.path(), &account).exists());
        assert_eq!(manager.startup(&origin).await.status, SessionStatus::SignedOut);
    }

    // @spec CLIENT-022 CLIENT-034
    #[tokio::test]
    async fn unavailable_credential_store_keeps_session_in_memory_only() {
        let idp = mock_idp().await;
        let portal = portal(&idp, vec![]).await;
        let dir = tempfile::tempdir().unwrap();
        let manager = manager(dir.path(), Arc::new(MemoryCredentialStore::unavailable()));
        let view = sign_in(&manager, &portal.server.origin()).await;
        assert_eq!(view.status, SessionStatus::UpToDate);
        assert_eq!(view.warning.as_deref(), Some(NOT_PERSISTED_WARNING));
        assert!(!dir.path().join(crate::cache::CACHE_DIR).exists());
    }

    // @spec CLIENT-032
    #[tokio::test]
    async fn cancelled_sign_in_rejects_the_callback() {
        let idp = mock_idp().await;
        let portal = portal(&idp, vec![]).await;
        let dir = tempfile::tempdir().unwrap();
        let manager = manager(dir.path(), Arc::new(MemoryCredentialStore::default()));
        let origin = portal.server.origin();
        let url = manager.begin_sign_in(&origin, DESKTOP_REDIRECT_URI).await.unwrap();
        let callback = authorize(&crate::http_client().unwrap(), &url, DESKTOP_REDIRECT_URI).await;
        manager.cancel_sign_in().await;
        assert_eq!(manager.complete_sign_in(&origin, &callback).await, Err(SignInError::Cancelled));
        assert!(idp.token_requests().is_empty());
    }
}
