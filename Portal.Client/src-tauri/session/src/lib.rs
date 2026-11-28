//! Native sign-in, authenticated manifest refresh, and encrypted manifest
//! cache for the Digital Fortress client (issue #12). Tauri-free so it can be
//! tested without a WebView.

pub mod cache;
pub mod credentials;
pub mod loopback;
pub mod manifest;
pub mod metadata;
pub mod oidc;
pub mod session;
#[cfg(test)]
mod test_server;

pub use credentials::{Account, CredentialError, CredentialStore, MemoryCredentialStore, StoredSession};
pub use loopback::CallbackListener;
pub use manifest::{Manifest, Site, SiteApp};
pub use oidc::{ANDROID_REDIRECT_URI, DESKTOP_REDIRECT_URI, SIGN_IN_TIMEOUT, SignInError};
pub use session::{SessionManager, SessionStatus, SessionView};

/// HTTP client for Portal and identity-provider requests: no redirects and a
/// bounded timeout.
pub fn http_client() -> reqwest::Result<reqwest::Client> {
    portal_onboarding::probe_client(portal_onboarding::PROBE_TIMEOUT)
}
