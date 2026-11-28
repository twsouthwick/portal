use std::time::{Duration, Instant};

use openidconnect::core::{
    CoreAuthenticationFlow, CoreClient, CoreProviderMetadata,
};
use openidconnect::{
    AuthorizationCode, ClientId, CsrfToken, EndpointMaybeSet, EndpointNotSet,
    EndpointSet, IssuerUrl, Nonce, OAuth2TokenResponse, PkceCodeChallenge, PkceCodeVerifier,
    RedirectUrl, RefreshToken, RequestTokenError, Scope, TokenResponse,
};
use reqwest::Client;
use url::Url;

use portal_onboarding::OriginPolicy;

use crate::metadata::{NativeOidcMetadata, is_secure_url};

/// Desktop loopback redirect URI.
pub const DESKTOP_REDIRECT_URI: &str = "http://127.0.0.1:49152/oidc-callback";
/// Android custom-scheme redirect URI.
pub const ANDROID_REDIRECT_URI: &str = "digital-fortress:/oidc-callback";
pub const SIGN_IN_TIMEOUT: Duration = Duration::from_secs(5 * 60);

type DiscoveredClient =
    CoreClient<EndpointSet, EndpointNotSet, EndpointNotSet, EndpointNotSet, EndpointMaybeSet, EndpointMaybeSet>;

#[derive(Debug, PartialEq, Eq, thiserror::Error)]
pub enum SignInError {
    #[error(transparent)]
    Metadata(#[from] crate::metadata::MetadataError),
    #[error("Could not load sign-in settings from the identity provider.")]
    Discovery,
    #[error("Sign-in was cancelled.")]
    Cancelled,
    #[error("Sign-in timed out. Try again.")]
    Expired,
    #[error("The sign-in response did not match this sign-in. Try again.")]
    StateMismatch,
    #[error("The identity provider did not complete sign-in ({0}).")]
    Provider(String),
    #[error("Could not complete sign-in with the identity provider.")]
    Token,
    #[error("Could not safely replace the saved sign-in. Try again.")]
    CredentialStore,
    #[error("Could not reach the identity provider.")]
    Unreachable,
    #[error("The identity provider returned an invalid sign-in.")]
    InvalidIdToken,
    #[error("Could not start sign-in: port 49152 on this device is in use.")]
    PortUnavailable,
}

/// A discovered issuer paired with the Portal's native client settings.
#[derive(Clone, Debug)]
pub struct OidcProvider {
    pub native: NativeOidcMetadata,
    provider: CoreProviderMetadata,
}

/// The single in-flight authorization transaction.
pub struct PendingSignIn {
    state: CsrfToken,
    nonce: Nonce,
    verifier: PkceCodeVerifier,
    redirect_uri: Url,
    expires_at: Instant,
}

impl PendingSignIn {
    pub fn is_expired(&self, now: Instant) -> bool {
        now >= self.expires_at
    }

    pub fn expires_at(&self) -> Instant {
        self.expires_at
    }

    #[cfg(test)]
    pub(crate) fn expire_after(&mut self, duration: Duration) {
        self.expires_at = Instant::now() + duration;
    }
}

/// Tokens from a code exchange or refresh. `subject` is set only for a
/// validated ID token.
#[derive(Clone, PartialEq, Eq)]
pub struct Tokens {
    pub issuer: String,
    pub subject: Option<String>,
    pub access_token: String,
    pub refresh_token: Option<String>,
    pub expires_at: Option<Instant>,
}

impl std::fmt::Debug for Tokens {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("Tokens")
            .field("issuer", &self.issuer)
            .field("subject", &self.subject)
            .finish_non_exhaustive()
    }
}

impl OidcProvider {
    /// @spec CLIENT-014
    pub async fn discover(
        http: &Client,
        native: NativeOidcMetadata,
        policy: OriginPolicy,
    ) -> Result<Self, SignInError> {
        let issuer = IssuerUrl::new(native.issuer.clone()).map_err(|_| SignInError::Discovery)?;
        // discover_async rejects a document whose issuer differs from `issuer`.
        let provider = CoreProviderMetadata::discover_async(issuer, http)
            .await
            .map_err(|_| SignInError::Discovery)?;
        let secure = is_secure_url(provider.authorization_endpoint().url(), policy)
            && provider
                .token_endpoint()
                .is_some_and(|token| is_secure_url(token.url(), policy))
            && is_secure_url(provider.jwks_uri().url(), policy);
        if !secure {
            return Err(SignInError::Discovery);
        }
        Ok(Self { native, provider })
    }

    fn client(&self, redirect_uri: &Url) -> Result<DiscoveredClient, SignInError> {
        Ok(CoreClient::from_provider_metadata(
            self.provider.clone(),
            ClientId::new(self.native.client_id.clone()),
            None,
        )
        .set_redirect_uri(
            RedirectUrl::new(redirect_uri.to_string()).map_err(|_| SignInError::Discovery)?,
        ))
    }

    /// Builds the authorization URL and its pending transaction.
    /// @spec CLIENT-015 CLIENT-036
    pub fn begin(&self, redirect_uri: &str) -> Result<(PendingSignIn, Url), SignInError> {
        let redirect_uri = Url::parse(redirect_uri).map_err(|_| SignInError::Discovery)?;
        let client = self.client(&redirect_uri)?;
        let (challenge, verifier) = PkceCodeChallenge::new_random_sha256();
        let mut request = client
            .authorize_url(
                CoreAuthenticationFlow::AuthorizationCode,
                CsrfToken::new_random,
                Nonce::new_random,
            )
            .set_pkce_challenge(challenge)
            .add_extra_param("audience", self.native.api_audience.clone());
        for scope in self.native.scopes.iter().filter(|scope| *scope != "openid") {
            request = request.add_scope(Scope::new(scope.clone()));
        }
        let (url, state, nonce) = request.url();
        Ok((
            PendingSignIn {
                state,
                nonce,
                verifier,
                redirect_uri,
                expires_at: Instant::now() + SIGN_IN_TIMEOUT,
            },
            url,
        ))
    }

    /// Validates the callback and exchanges its code.
    /// @spec CLIENT-015 CLIENT-036 CLIENT-037
    pub async fn complete(
        &self,
        http: &Client,
        pending: PendingSignIn,
        callback: &Url,
    ) -> Result<Tokens, SignInError> {
        if pending.is_expired(Instant::now()) {
            return Err(SignInError::Expired);
        }
        let same_endpoint = callback.scheme() == pending.redirect_uri.scheme()
            && callback.host_str() == pending.redirect_uri.host_str()
            && callback.port_or_known_default() == pending.redirect_uri.port_or_known_default()
            && callback.path() == pending.redirect_uri.path();
        if !same_endpoint {
            return Err(SignInError::StateMismatch);
        }
        let param = |name: &str| {
            callback
                .query_pairs()
                .find(|(key, _)| key == name)
                .map(|(_, value)| value.into_owned())
        };
        if param("state").as_deref() != Some(pending.state.secret().as_str()) {
            return Err(SignInError::StateMismatch);
        }
        if let Some(error) = param("error") {
            let error: String = error
                .chars()
                .filter(|c| c.is_ascii_alphanumeric() || *c == '_')
                .take(64)
                .collect();
            return Err(SignInError::Provider(error));
        }
        let code = param("code").ok_or(SignInError::Token)?;

        let client = self.client(&pending.redirect_uri)?;
        let response = client
            .exchange_code(AuthorizationCode::new(code))
            .map_err(|_| SignInError::Token)?
            .set_pkce_verifier(pending.verifier)
            .request_async(http)
            .await
            .map_err(|_| SignInError::Token)?;
        let id_token = response.id_token().ok_or(SignInError::InvalidIdToken)?;
        let claims = id_token
            .claims(&client.id_token_verifier(), &pending.nonce)
            .map_err(|_| SignInError::InvalidIdToken)?;
        Ok(Tokens {
            issuer: claims.issuer().to_string(),
            subject: Some(claims.subject().to_string()),
            access_token: response.access_token().secret().clone(),
            refresh_token: response.refresh_token().map(|token| token.secret().clone()),
            expires_at: response.expires_in().map(|ttl| Instant::now() + ttl),
        })
    }

    /// @spec CLIENT-020
    pub async fn refresh(&self, http: &Client, refresh_token: &str) -> Result<Tokens, SignInError> {
        let redirect = Url::parse(DESKTOP_REDIRECT_URI).expect("valid redirect");
        let client = self.client(&redirect)?;
        let refresh_token = RefreshToken::new(refresh_token.to_owned());
        let response = client
            .exchange_refresh_token(&refresh_token)
            .map_err(|_| SignInError::Token)?
            .request_async(http)
            .await
            .map_err(|error| match error {
                RequestTokenError::Request(_) => SignInError::Unreachable,
                _ => SignInError::Token,
            })?;
        Ok(Tokens {
            issuer: self.native.issuer.clone(),
            subject: None,
            access_token: response.access_token().secret().clone(),
            refresh_token: response.refresh_token().map(|token| token.secret().clone()),
            expires_at: response.expires_in().map(|ttl| Instant::now() + ttl),
        })
    }
}

#[cfg(test)]
pub(crate) mod tests {
    use super::*;
    use crate::test_server::{Request, TestServer, json};
    use chrono::Utc;
    use openidconnect::core::{
        CoreIdToken, CoreIdTokenClaims, CoreJsonWebKeySet, CoreJwsSigningAlgorithm,
        CoreRsaPrivateSigningKey,
    };
    use openidconnect::{
        Audience, EmptyAdditionalClaims, JsonWebKeyId, PrivateSigningKey, StandardClaims,
        SubjectIdentifier,
    };
    use rsa::pkcs1::{EncodeRsaPrivateKey, LineEnding};
    use std::sync::{Arc, Mutex, OnceLock};

    fn key_pem() -> &'static str {
        static PEM: OnceLock<String> = OnceLock::new();
        PEM.get_or_init(|| {
            let key = rsa::RsaPrivateKey::new(&mut rand::rngs::OsRng, 2048).unwrap();
            key.to_pkcs1_pem(LineEnding::LF).unwrap().to_string()
        })
    }

    fn signing_key() -> CoreRsaPrivateSigningKey {
        CoreRsaPrivateSigningKey::from_pem(key_pem(), Some(JsonWebKeyId::new("k1".into()))).unwrap()
    }

    /// Settings a mock IdP uses when issuing tokens.
    #[derive(Clone, Default)]
    pub struct IdpBehavior {
        pub nonce_override: Option<String>,
        pub audience_override: Option<String>,
        pub refresh_fails: bool,
        pub token_delay: Duration,
    }

    pub struct MockIdp {
        pub server: TestServer,
        pub behavior: Arc<Mutex<IdpBehavior>>,
    }

    impl MockIdp {
        pub fn issuer(&self) -> String {
            self.server.base.clone()
        }

        pub fn native(&self) -> NativeOidcMetadata {
            NativeOidcMetadata {
                issuer: self.issuer(),
                client_id: "portal-native".into(),
                api_audience: "portal-api".into(),
                scopes: vec!["openid".into(), "groups".into(), "offline_access".into()],
            }
        }

        pub fn token_requests(&self) -> Vec<Request> {
            self.server.requests().into_iter().filter(|r| r.path == "/token").collect()
        }
    }

    pub async fn mock_idp() -> MockIdp {
        // Debug-build RSA keygen is slow; finish it before serving JWKS.
        tokio::task::spawn_blocking(key_pem).await.unwrap();
        let behavior = Arc::new(Mutex::new(IdpBehavior::default()));
        let issued = Arc::new(Mutex::new(Vec::<String>::new()));
        let issuer_cell = Arc::new(OnceLock::<String>::new());
        let nonces = Arc::new(Mutex::new(std::collections::HashMap::<String, String>::new()));
        let (b, i, cell, n) = (behavior.clone(), issued.clone(), issuer_cell.clone(), nonces.clone());
        let server = TestServer::start(move |request| {
            let issuer = cell.get().unwrap().clone();
            match request.path.as_str() {
                p if p.ends_with("/.well-known/openid-configuration") => json(
                    200,
                    serde_json::json!({
                        "issuer": issuer,
                        "authorization_endpoint": format!("{issuer}/authorize"),
                        "token_endpoint": format!("{issuer}/token"),
                        "jwks_uri": format!("{issuer}/jwks"),
                        "response_types_supported": ["code"],
                        "subject_types_supported": ["public"],
                        "id_token_signing_alg_values_supported": ["RS256"]
                    })
                    .to_string(),
                ),
                "/jwks" => json(
                    200,
                    serde_json::to_string(&CoreJsonWebKeySet::new(vec![
                        signing_key().as_verification_key(),
                    ]))
                    .unwrap(),
                ),
                // Test hook: remember the nonce for an authorization code.
                "/authorize" => {
                    let query: std::collections::HashMap<String, String> =
                        url::form_urlencoded::parse(request.query.as_bytes()).into_owned().collect();
                    n.lock().unwrap().insert("code-1".into(), query["nonce"].clone());
                    (200, vec![], String::new())
                }
                "/token" => {
                    let behavior = b.lock().unwrap().clone();
                    let form = request.form();
                    let access = format!("access-{}", i.lock().unwrap().len() + 1);
                    match form.get("grant_type").map(String::as_str) {
                        Some("authorization_code") => {
                            std::thread::sleep(behavior.token_delay);
                            if !form.contains_key("code_verifier") {
                                return json(400, r#"{"error":"invalid_grant"}"#);
                            }
                            let nonce = behavior
                                .nonce_override
                                .clone()
                                .or_else(|| n.lock().unwrap().get(&form["code"]).cloned())
                                .unwrap_or_default();
                            let audience = behavior
                                .audience_override
                                .clone()
                                .unwrap_or_else(|| "portal-native".into());
                            let claims = CoreIdTokenClaims::new(
                                IssuerUrl::new(issuer.clone()).unwrap(),
                                vec![Audience::new(audience)],
                                Utc::now() + chrono::Duration::minutes(5),
                                Utc::now(),
                                StandardClaims::new(SubjectIdentifier::new("alice".into())),
                                EmptyAdditionalClaims {},
                            )
                            .set_nonce(Some(Nonce::new(nonce)));
                            let id_token = CoreIdToken::new(
                                claims,
                                &signing_key(),
                                CoreJwsSigningAlgorithm::RsaSsaPkcs1V15Sha256,
                                None,
                                None,
                            )
                            .unwrap();
                            i.lock().unwrap().push(access.clone());
                            json(
                                200,
                                serde_json::json!({
                                    "access_token": access,
                                    "token_type": "Bearer",
                                    "expires_in": 3600,
                                    "refresh_token": "refresh-1",
                                    "id_token": id_token.to_string(),
                                })
                                .to_string(),
                            )
                        }
                        Some("refresh_token") if !behavior.refresh_fails => {
                            i.lock().unwrap().push(access.clone());
                            json(
                                200,
                                serde_json::json!({
                                    "access_token": access,
                                    "token_type": "Bearer",
                                    "expires_in": 3600,
                                    "refresh_token": "refresh-2",
                                })
                                .to_string(),
                            )
                        }
                        _ => json(400, r#"{"error":"invalid_grant"}"#),
                    }
                }
                _ => (404, vec![], String::new()),
            }
        })
        .await;
        issuer_cell.set(server.base.clone()).unwrap();
        MockIdp { server, behavior }
    }

    /// Simulates the browser: visits the authorization URL and returns the
    /// redirect the IdP would send.
    pub async fn authorize(http: &Client, url: &Url, redirect_uri: &str) -> Url {
        http.get(url.clone()).send().await.unwrap();
        let state = url.query_pairs().find(|(k, _)| k == "state").unwrap().1.into_owned();
        Url::parse(&format!("{redirect_uri}?code=code-1&state={state}")).unwrap()
    }

    async fn provider(idp: &MockIdp) -> OidcProvider {
        OidcProvider::discover(&crate::http_client().unwrap(), idp.native(), OriginPolicy::AllowHttpLoopback)
            .await
            .unwrap()
    }

    // @spec CLIENT-015 CLIENT-034 CLIENT-036
    #[tokio::test]
    async fn authorization_url_uses_pkce_state_nonce_scopes_and_audience() {
        let idp = mock_idp().await;
        let (_, url) = provider(&idp).await.begin(DESKTOP_REDIRECT_URI).unwrap();
        let query: std::collections::HashMap<String, String> = url.query_pairs().into_owned().collect();
        assert_eq!(DESKTOP_REDIRECT_URI, "http://127.0.0.1:49152/oidc-callback");
        assert_eq!(query["response_type"], "code");
        assert_eq!(query["client_id"], "portal-native");
        assert_eq!(query["redirect_uri"], DESKTOP_REDIRECT_URI);
        assert_eq!(query["code_challenge_method"], "S256");
        assert_eq!(query["audience"], "portal-api");
        assert_eq!(query["scope"], "openid groups offline_access");
        assert!(!query["state"].is_empty() && !query["nonce"].is_empty());
        assert!(!query.contains_key("client_secret"));
    }

    // @spec CLIENT-015 CLIENT-037
    #[tokio::test]
    async fn completes_sign_in_with_validated_id_token() {
        let idp = mock_idp().await;
        let http = crate::http_client().unwrap();
        let provider = provider(&idp).await;
        let (pending, url) = provider.begin(ANDROID_REDIRECT_URI).unwrap();
        let callback = authorize(&http, &url, ANDROID_REDIRECT_URI).await;
        let tokens = provider.complete(&http, pending, &callback).await.unwrap();
        assert_eq!(tokens.issuer, idp.issuer());
        assert_eq!(tokens.subject.as_deref(), Some("alice"));
        assert_eq!(tokens.access_token, "access-1");
        assert_eq!(tokens.refresh_token.as_deref(), Some("refresh-1"));
        assert!(!format!("{tokens:?}").contains("access-1"));
    }

    // @spec CLIENT-036
    #[tokio::test]
    async fn rejects_mismatched_state_expired_transaction_and_provider_error() {
        let idp = mock_idp().await;
        let http = crate::http_client().unwrap();
        let provider = provider(&idp).await;

        let (pending, _) = provider.begin(DESKTOP_REDIRECT_URI).unwrap();
        let forged = Url::parse(&format!("{DESKTOP_REDIRECT_URI}?code=code-1&state=forged")).unwrap();
        assert_eq!(provider.complete(&http, pending, &forged).await, Err(SignInError::StateMismatch));

        let (mut pending, url) = provider.begin(DESKTOP_REDIRECT_URI).unwrap();
        let callback = authorize(&http, &url, DESKTOP_REDIRECT_URI).await;
        pending.expire_after(Duration::ZERO);
        assert_eq!(provider.complete(&http, pending, &callback).await, Err(SignInError::Expired));

        let (pending, url) = provider.begin(DESKTOP_REDIRECT_URI).unwrap();
        let state = url.query_pairs().find(|(k, _)| k == "state").unwrap().1.into_owned();
        let denied = Url::parse(&format!("{DESKTOP_REDIRECT_URI}?error=access_denied&state={state}")).unwrap();
        assert_eq!(
            provider.complete(&http, pending, &denied).await,
            Err(SignInError::Provider("access_denied".into()))
        );
        assert!(idp.token_requests().is_empty());
    }

    // @spec CLIENT-037
    #[tokio::test]
    async fn rejects_id_token_with_wrong_nonce_or_audience() {
        let idp = mock_idp().await;
        let http = crate::http_client().unwrap();
        let provider = provider(&idp).await;

        idp.behavior.lock().unwrap().nonce_override = Some("other".into());
        let (pending, url) = provider.begin(DESKTOP_REDIRECT_URI).unwrap();
        let callback = authorize(&http, &url, DESKTOP_REDIRECT_URI).await;
        assert_eq!(provider.complete(&http, pending, &callback).await, Err(SignInError::InvalidIdToken));

        *idp.behavior.lock().unwrap() = IdpBehavior {
            audience_override: Some("someone-else".into()),
            ..Default::default()
        };
        let (pending, url) = provider.begin(DESKTOP_REDIRECT_URI).unwrap();
        let callback = authorize(&http, &url, DESKTOP_REDIRECT_URI).await;
        assert_eq!(provider.complete(&http, pending, &callback).await, Err(SignInError::InvalidIdToken));
    }

    // @spec CLIENT-014
    #[tokio::test]
    async fn rejects_discovery_with_mismatched_issuer() {
        let idp = mock_idp().await;
        let mut native = idp.native();
        native.issuer = format!("{}/other", idp.issuer());
        let result =
            OidcProvider::discover(&crate::http_client().unwrap(), native, OriginPolicy::AllowHttpLoopback).await;
        assert_eq!(result.err(), Some(SignInError::Discovery));
    }
}
