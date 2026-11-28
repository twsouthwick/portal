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
    cached_manifest: Option<CachedManifest>,
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
    pending_id: Option<u64>,
    next_pending_id: u64,
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
        active.cached_manifest.clone().or_else(|| {
            read_cache(&self.data_dir, &active.stored.cache_key, &active.stored.account)
        })
    }

    fn load(&self, state: &mut State) {
        if state.loaded {
            return;
        }
        state.loaded = true;
        if let Ok(Some(stored)) = self.store.load() {
            let cached_manifest = read_cache(&self.data_dir, &stored.cache_key, &stored.account);
            state.active = Some(Active {
                stored,
                access_token: None,
                expires_at: None,
                persisted: true,
                cached_manifest,
            });
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
        let account_issuer = state
            .active
            .as_ref()
            .expect("active session")
            .stored
            .account
            .issuer
            .clone();
        if provider.native.issuer != account_issuer {
            state.active.as_mut().expect("active session").access_token = None;
            return Ok(None);
        }
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

    /// @spec CLIENT-016 CLIENT-018 CLIENT-019 CLIENT-020 CLIENT-043
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
            match outcome {
                FetchOutcome::Updated { manifest, etag } => {
                    let active = state.active.as_mut().expect("active session");
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
                        let active = state.active.as_mut().expect("active session");
                        return self.store_and_view(active, existing);
                    }
                    other if !retried_not_modified => {
                        cache = other;
                        retried_not_modified = true;
                    }
                    other => {
                        let active = state.active.as_ref().expect("active session");
                        return self.view(active, SessionStatus::Failed, other);
                    }
                },
                FetchOutcome::Unauthorized if !retried_unauthorized => retried_unauthorized = true,
                FetchOutcome::Unauthorized => {
                    let active = state.active.as_mut().expect("active session");
                    active.access_token = None;
                    let active = state.active.as_ref().expect("active session");
                    return self.view(active, SessionStatus::SignInRequired, cache);
                }
                FetchOutcome::Failed(FetchFailure::Network) => {
                    let active = state.active.as_ref().expect("active session");
                    return self.view(active, SessionStatus::Offline, cache);
                }
                FetchOutcome::Failed(_) => {
                    let active = state.active.as_ref().expect("active session");
                    return self.view(active, SessionStatus::Failed, cache);
                }
            }
        }
    }

    fn store_and_view(&self, active: &mut Active, cached: CachedManifest) -> SessionView {
        active.cached_manifest = Some(cached.clone());
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
        state.pending_id = None;
        // Re-fetch metadata for each sign-in.
        state.provider = None;
        let provider = self.provider(&mut state, origin).await?;
        let (pending, url) = provider.begin(redirect_uri)?;
        state.next_pending_id = state.next_pending_id.wrapping_add(1);
        state.pending_id = Some(state.next_pending_id);
        state.pending = Some(pending);
        Ok(url)
    }

    /// @spec CLIENT-032
    pub async fn cancel_sign_in(&self) {
        let mut state = self.state.lock().await;
        state.pending = None;
        state.pending_id = None;
    }

    /// Completes sign-in from the callback, persists the session, and loads
    /// the manifest.
    /// @spec CLIENT-015 CLIENT-032 CLIENT-033 CLIENT-034
    pub async fn complete_sign_in(&self, origin: &PortalOrigin, callback: &Url) -> Result<SessionView, SignInError> {
        let (transaction_id, pending, expires_at, provider) = {
            let mut state = self.state.lock().await;
            self.load(&mut state);
            let pending = state.pending.take().ok_or(SignInError::Cancelled)?;
            let transaction_id = state.pending_id.ok_or(SignInError::Cancelled)?;
            if pending.is_expired(Instant::now()) {
                state.pending_id = None;
                return Err(SignInError::Expired);
            }
            let provider = match &state.provider {
                Some((cached_origin, provider)) if cached_origin == origin.as_str() => provider.clone(),
                _ => {
                    state.pending_id = None;
                    return Err(SignInError::Cancelled);
                }
            };
            let expires_at = pending.expires_at();
            (transaction_id, pending, expires_at, provider)
        };
        let tokens = match tokio::time::timeout_at(
            tokio::time::Instant::from_std(expires_at),
            provider.complete(&self.http, pending, callback),
        )
        .await
        {
            Ok(Ok(tokens)) => tokens,
            Ok(Err(error)) => {
                let mut state = self.state.lock().await;
                if state.pending_id == Some(transaction_id) {
                    state.pending_id = None;
                }
                return Err(error);
            }
            Err(_) => {
                let mut state = self.state.lock().await;
                if state.pending_id == Some(transaction_id) {
                    state.pending_id = None;
                }
                return Err(SignInError::Expired);
            }
        };
        let mut state = self.state.lock().await;
        if state.pending_id != Some(transaction_id) {
            return Err(SignInError::Cancelled);
        }
        if expires_at <= Instant::now() {
            state.pending_id = None;
            return Err(SignInError::Expired);
        }
        state.pending_id = None;
        let account = Account {
            origin: origin.as_str().to_owned(),
            issuer: tokens.issuer,
            subject: tokens.subject.ok_or(SignInError::InvalidIdToken)?,
        };
        let same_account = state
            .active
            .as_ref()
            .is_some_and(|active| active.stored.account == account);
        let (cache_key, cached_manifest) = match state.active.as_ref() {
            Some(previous) if same_account => (
                previous.stored.cache_key.clone(),
                previous.cached_manifest.clone(),
            ),
            _ => (CacheKey::generate().map_err(|_| SignInError::Token)?, None),
        };
        let stored = StoredSession { account: account.clone(), refresh_token: tokens.refresh_token, cache_key };
        let persisted = match self.store.save(&stored) {
            Ok(()) => true,
            Err(_) => {
                let replacing_persisted_account = state.active.as_ref().is_some_and(|previous| {
                    previous.persisted && previous.stored.account != account
                });
                if replacing_persisted_account {
                    self.store.delete().map_err(|_| SignInError::CredentialStore)?;
                }
                false
            }
        };
        if let Some(previous) = state.active.as_ref()
            && previous.stored.account != account
        {
            let _ = delete_cache(&self.data_dir, &previous.stored.account);
        }
        state.active = Some(Active {
            stored,
            access_token: Some(tokens.access_token),
            expires_at: tokens.expires_at,
            persisted,
            cached_manifest,
        });
        drop(state);
        Ok(self.refresh(origin).await)
    }

    /// Clears the account's cache and credentials.
    /// @spec CLIENT-021
    pub async fn sign_out(&self) -> SessionView {
        let mut state = self.state.lock().await;
        self.load(&mut state);
        state.pending = None;
        state.pending_id = None;
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

    #[derive(Default)]
    struct FaultingCredentialStore {
        inner: MemoryCredentialStore,
        fail_save: StdMutex<bool>,
        fail_delete: StdMutex<bool>,
    }

    impl FaultingCredentialStore {
        fn set_failures(&self, save: bool, delete: bool) {
            *self.fail_save.lock().unwrap() = save;
            *self.fail_delete.lock().unwrap() = delete;
        }
    }

    impl CredentialStore for FaultingCredentialStore {
        fn load(&self) -> Result<Option<StoredSession>, crate::credentials::CredentialError> {
            self.inner.load()
        }

        fn save(&self, session: &StoredSession) -> Result<(), crate::credentials::CredentialError> {
            if *self.fail_save.lock().unwrap() {
                return Err(crate::credentials::CredentialError("save failed".into()));
            }
            self.inner.save(session)
        }

        fn delete(&self) -> Result<(), crate::credentials::CredentialError> {
            if *self.fail_delete.lock().unwrap() {
                return Err(crate::credentials::CredentialError("delete failed".into()));
            }
            self.inner.delete()
        }
    }

    /// Portal script per manifest request; metadata always served.
    struct Portal {
        server: TestServer,
        script: Arc<StdMutex<Script>>,
        issuer: Arc<StdMutex<String>>,
    }

    async fn portal(idp: &MockIdp, script: Script) -> Portal {
        portal_with_issuer(idp.issuer(), script).await
    }

    async fn portal_with_issuer(issuer: String, script: Script) -> Portal {
        let script = Arc::new(StdMutex::new(script));
        let issuer = Arc::new(StdMutex::new(issuer));
        let metadata_issuer = issuer.clone();
        let s = script.clone();
        let server = TestServer::start(move |request| match request.path.as_str() {
            "/.well-known/portal-native-oidc" => json(
                200,
                serde_json::json!({
                    "issuer": metadata_issuer.lock().unwrap().clone(), "clientId": "portal-native",
                    "apiAudience": "portal-api", "scopes": ["openid", "offline_access"]
                })
                .to_string(),
            ),
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
        Portal { server, script, issuer }
    }

    impl Portal {
        fn set_issuer(&self, issuer: String) {
            *self.issuer.lock().unwrap() = issuer;
        }

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

    // @spec CLIENT-043
    #[tokio::test]
    async fn restart_does_not_send_refresh_token_to_a_changed_issuer() {
        let original_idp = mock_idp().await;
        let changed_idp = mock_idp().await;
        let portal = portal(&original_idp, vec![(200, Some("\"v1\""))]).await;
        let dir = tempfile::tempdir().unwrap();
        let store: Arc<dyn CredentialStore> = Arc::new(MemoryCredentialStore::default());
        let origin = portal.server.origin();
        sign_in(&manager(dir.path(), store.clone()), &origin).await;

        portal.set_issuer(changed_idp.issuer());
        let restarted = manager(dir.path(), store);
        assert_eq!(restarted.startup(&origin).await.manifest.unwrap().name, "Home");
        let view = restarted.refresh(&origin).await;

        assert_eq!(view.status, SessionStatus::SignInRequired);
        assert_eq!(view.manifest.unwrap().name, "Home");
        assert_eq!(
            original_idp.token_requests().len(),
            1,
            "original issuer should see only the sign-in exchange"
        );
        assert!(
            changed_idp.token_requests().is_empty(),
            "changed issuer must not receive the stored refresh token"
        );
        assert_eq!(
            portal.manifest_requests().len(),
            1,
            "manifest request must wait for fresh sign-in"
        );
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
        let portal = portal(&idp, vec![(200, Some("\"v1\"")), (503, None), (503, None)]).await;
        let dir = tempfile::tempdir().unwrap();
        let manager = manager(dir.path(), Arc::new(MemoryCredentialStore::unavailable()));
        let view = sign_in(&manager, &portal.server.origin()).await;
        assert_eq!(view.status, SessionStatus::UpToDate);
        assert_eq!(view.warning.as_deref(), Some(NOT_PERSISTED_WARNING));
        assert!(!dir.path().join(crate::cache::CACHE_DIR).exists());

        let failed = manager.refresh(&portal.server.origin()).await;
        assert_eq!(failed.status, SessionStatus::Failed);
        assert_eq!(failed.manifest.as_ref().unwrap().name, "Home");
        assert_eq!(manager.refresh(&portal.server.origin()).await.manifest.unwrap().name, "Home");
        assert_eq!(portal.manifest_requests()[2].header("if-none-match"), Some("\"v1\""));
    }

    // @spec CLIENT-019
    #[tokio::test]
    async fn keeps_new_manifest_in_memory_when_cache_write_fails() {
        let idp = mock_idp().await;
        let portal = portal(
            &idp,
            vec![(200, Some("\"v1\"")), (200, Some("\"v2\"")), (503, None)],
        )
        .await;
        let dir = tempfile::tempdir().unwrap();
        let manager = manager(dir.path(), Arc::new(MemoryCredentialStore::default()));
        let origin = portal.server.origin();
        sign_in(&manager, &origin).await;

        let cache_dir = dir.path().join(crate::cache::CACHE_DIR);
        std::fs::remove_dir_all(&cache_dir).unwrap();
        std::fs::write(&cache_dir, "not a directory").unwrap();

        let updated = manager.refresh(&origin).await;
        assert_eq!(updated.status, SessionStatus::UpToDate);
        assert_eq!(updated.manifest.as_ref().unwrap().name, "Home");
        assert!(updated.warning.is_some());

        let failed = manager.refresh(&origin).await;
        assert_eq!(failed.status, SessionStatus::Failed);
        assert_eq!(failed.manifest.unwrap().name, "Home");
        assert_eq!(portal.manifest_requests()[2].header("if-none-match"), Some("\"v2\""));
    }

    // @spec CLIENT-034
    #[tokio::test]
    async fn failed_account_save_invalidates_previous_persisted_session() {
        let idp = mock_idp().await;
        let portal_a = portal(&idp, vec![]).await;
        let portal_b = portal(&idp, vec![]).await;
        let dir = tempfile::tempdir().unwrap();
        let store = Arc::new(FaultingCredentialStore::default());
        let session = manager(dir.path(), store.clone());
        let origin_a = portal_a.server.origin();
        let origin_b = portal_b.server.origin();
        sign_in(&session, &origin_a).await;
        assert!(store.load().unwrap().is_some());

        store.set_failures(true, false);
        let memory_only = sign_in(&session, &origin_b).await;

        assert_eq!(memory_only.status, SessionStatus::UpToDate);
        assert_eq!(memory_only.warning.as_deref(), Some(NOT_PERSISTED_WARNING));
        assert!(store.load().unwrap().is_none());
        let restarted = manager(dir.path(), store);
        assert_eq!(restarted.startup(&origin_a).await.status, SessionStatus::SignedOut);
        assert_eq!(restarted.startup(&origin_b).await.status, SessionStatus::SignedOut);
    }

    // @spec CLIENT-034
    #[tokio::test]
    async fn failed_credential_invalidation_rejects_account_switch() {
        let idp = mock_idp().await;
        let portal_a = portal(&idp, vec![]).await;
        let portal_b = portal(&idp, vec![]).await;
        let dir = tempfile::tempdir().unwrap();
        let store = Arc::new(FaultingCredentialStore::default());
        let session = manager(dir.path(), store.clone());
        let origin_a = portal_a.server.origin();
        let origin_b = portal_b.server.origin();
        sign_in(&session, &origin_a).await;
        let previous = store.load().unwrap();
        store.set_failures(true, true);

        let url = session.begin_sign_in(&origin_b, DESKTOP_REDIRECT_URI).await.unwrap();
        let callback = authorize(&crate::http_client().unwrap(), &url, DESKTOP_REDIRECT_URI).await;
        assert_eq!(
            session.complete_sign_in(&origin_b, &callback).await,
            Err(SignInError::CredentialStore)
        );

        assert_eq!(store.load().unwrap(), previous);
        assert_eq!(session.startup(&origin_a).await.status, SessionStatus::Refreshing);
        assert_eq!(session.startup(&origin_b).await.status, SessionStatus::SignedOut);
    }

    // @spec CLIENT-032
    #[tokio::test(flavor = "multi_thread", worker_threads = 2)]
    async fn cancellation_during_token_exchange_prevents_session_commit() {
        let idp = mock_idp().await;
        idp.behavior.lock().unwrap().token_delay = Duration::from_millis(100);
        let portal = portal(&idp, vec![]).await;
        let dir = tempfile::tempdir().unwrap();
        let store = Arc::new(MemoryCredentialStore::default());
        let manager = Arc::new(manager(dir.path(), store.clone()));
        let origin = portal.server.origin();
        let url = manager.begin_sign_in(&origin, DESKTOP_REDIRECT_URI).await.unwrap();
        let callback = authorize(&crate::http_client().unwrap(), &url, DESKTOP_REDIRECT_URI).await;
        let completing = {
            let manager = manager.clone();
            tokio::spawn(async move { manager.complete_sign_in(&origin, &callback).await })
        };
        while idp.token_requests().is_empty() {
            tokio::task::yield_now().await;
        }
        manager.cancel_sign_in().await;

        assert_eq!(completing.await.unwrap(), Err(SignInError::Cancelled));
        assert!(store.load().unwrap().is_none());
    }

    // @spec CLIENT-032
    #[tokio::test(flavor = "multi_thread", worker_threads = 2)]
    async fn expiry_during_token_exchange_prevents_session_commit() {
        let idp = mock_idp().await;
        idp.behavior.lock().unwrap().token_delay = Duration::from_millis(100);
        let portal = portal(&idp, vec![]).await;
        let dir = tempfile::tempdir().unwrap();
        let store = Arc::new(MemoryCredentialStore::default());
        let session = Arc::new(manager(dir.path(), store.clone()));
        let origin = portal.server.origin();
        let url = session.begin_sign_in(&origin, DESKTOP_REDIRECT_URI).await.unwrap();
        let callback = authorize(&crate::http_client().unwrap(), &url, DESKTOP_REDIRECT_URI).await;
        {
            let mut state = session.state.lock().await;
            state.pending.as_mut().unwrap().expire_after(Duration::from_millis(30));
        }
        let completing = {
            let session = session.clone();
            tokio::spawn(async move { session.complete_sign_in(&origin, &callback).await })
        };
        while idp.token_requests().is_empty() {
            tokio::task::yield_now().await;
        }

        assert_eq!(completing.await.unwrap(), Err(SignInError::Expired));
        assert!(store.load().unwrap().is_none());
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
