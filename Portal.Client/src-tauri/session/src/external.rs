use serde::Deserialize;
use url::Url;

/// What the UI asks the host to open.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum ExternalKind {
    /// A site, Markdown link, or store page: `http` or `https` only.
    Web,
    /// An app launch URI.
    App,
}

const BLOCKED_APP_SCHEMES: [&str; 4] = ["javascript", "data", "file", "vbscript"];

/// Returns the URL to hand to the system opener, or `None` when it must stay inert.
/// @spec CLIENT-043 CLIENT-044 CLIENT-045 CLIENT-046
pub fn validate_external(input: &str, kind: ExternalKind) -> Option<Url> {
    let url = Url::parse(input.trim()).ok()?;
    let allowed = match kind {
        ExternalKind::Web => matches!(url.scheme(), "http" | "https") && url.host().is_some(),
        ExternalKind::App => !BLOCKED_APP_SCHEMES.contains(&url.scheme()),
    };
    allowed.then_some(url)
}

#[cfg(test)]
mod tests {
    use super::*;

    // @spec CLIENT-043 CLIENT-044 CLIENT-046
    #[test]
    fn web_accepts_only_http_and_https() {
        assert!(validate_external("https://media.example/", ExternalKind::Web).is_some());
        assert!(validate_external("http://media.example/x", ExternalKind::Web).is_some());
        for input in ["javascript:alert(1)", "file:///etc/passwd", "mailto:a@b", "plex://x", "not a url"] {
            assert!(validate_external(input, ExternalKind::Web).is_none(), "{input}");
        }
    }

    // @spec CLIENT-045
    #[test]
    fn app_rejects_dangerous_schemes() {
        assert!(validate_external("plex://open", ExternalKind::App).is_some());
        assert!(validate_external("https://app.example/", ExternalKind::App).is_some());
        for input in ["javascript:alert(1)", "JavaScript:alert(1)", "data:text/html,x", "file:///x", "vbscript:x", "nope"] {
            assert!(validate_external(input, ExternalKind::App).is_none(), "{input}");
        }
    }
}
