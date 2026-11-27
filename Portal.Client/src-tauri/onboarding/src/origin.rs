use std::fmt;
use std::net::IpAddr;

use serde::Serialize;
use url::{Host, Url};

/// Which schemes an entered Portal URL may use.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum OriginPolicy {
    HttpsOnly,
    /// Development only: also accept `http` for loopback hosts.
    AllowHttpLoopback,
}

/// A normalized, untrusted Portal origin (`scheme://host[:port]`).
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct PortalOrigin(Url);

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum OriginError {
    Empty,
    Invalid,
    UnsupportedScheme,
    InsecureScheme,
    CredentialsNotAllowed,
    NotAnOrigin,
}

impl fmt::Display for OriginError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(match self {
            Self::Empty => "Enter the Portal URL.",
            Self::Invalid => "The Portal URL is not valid.",
            Self::UnsupportedScheme => "The Portal URL must use https.",
            Self::InsecureScheme => "The Portal URL must use https.",
            Self::CredentialsNotAllowed => {
                "The Portal URL must not contain a user name or password."
            }
            Self::NotAnOrigin => {
                "Enter only the Portal address, without a path, query, or fragment."
            }
        })
    }
}

impl std::error::Error for OriginError {}

impl PortalOrigin {
    /// @spec CLIENT-008 CLIENT-009
    pub fn parse(input: &str, policy: OriginPolicy) -> Result<Self, OriginError> {
        let input = input.trim();
        if input.is_empty() {
            return Err(OriginError::Empty);
        }
        let candidate = if input.contains("://") {
            input.to_owned()
        } else {
            format!("https://{input}")
        };
        let url = Url::parse(&candidate).map_err(|_| OriginError::Invalid)?;

        match url.scheme() {
            "https" => {}
            "http" if policy == OriginPolicy::AllowHttpLoopback && is_loopback(&url) => {}
            "http" => return Err(OriginError::InsecureScheme),
            _ => return Err(OriginError::UnsupportedScheme),
        }
        if !url.username().is_empty() || url.password().is_some() {
            return Err(OriginError::CredentialsNotAllowed);
        }
        if url.host().is_none() {
            return Err(OriginError::Invalid);
        }
        if url.path() != "/" || url.query().is_some() || url.fragment().is_some() {
            return Err(OriginError::NotAnOrigin);
        }

        let origin = url.origin().ascii_serialization();
        Url::parse(&origin)
            .map(Self)
            .map_err(|_| OriginError::Invalid)
    }

    pub fn as_str(&self) -> &str {
        self.0.as_str().trim_end_matches('/')
    }

    pub(crate) fn join(&self, path: &str) -> Url {
        self.0
            .join(path)
            .expect("a relative path joins onto an origin")
    }
}

impl fmt::Display for PortalOrigin {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(self.as_str())
    }
}

fn is_loopback(url: &Url) -> bool {
    match url.host() {
        Some(Host::Domain(domain)) => domain.eq_ignore_ascii_case("localhost"),
        Some(Host::Ipv4(ip)) => IpAddr::V4(ip).is_loopback(),
        Some(Host::Ipv6(ip)) => IpAddr::V6(ip).is_loopback(),
        None => false,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn https(input: &str) -> Result<String, OriginError> {
        PortalOrigin::parse(input, OriginPolicy::HttpsOnly).map(|o| o.to_string())
    }

    fn dev(input: &str) -> Result<String, OriginError> {
        PortalOrigin::parse(input, OriginPolicy::AllowHttpLoopback).map(|o| o.to_string())
    }

    // @spec CLIENT-008
    #[test]
    fn normalizes_to_origin() {
        assert_eq!(
            https("  portal.example.com "),
            Ok("https://portal.example.com".into())
        );
        assert_eq!(
            https("HTTPS://Portal.Example.COM:443/"),
            Ok("https://portal.example.com".into())
        );
        assert_eq!(
            https("https://portal.example.com:8443"),
            Ok("https://portal.example.com:8443".into())
        );
        assert_eq!(
            https("https://bücher.example"),
            Ok("https://xn--bcher-kva.example".into())
        );
        assert_eq!(https("https://[::1]:5001"), Ok("https://[::1]:5001".into()));
    }

    // @spec CLIENT-008
    #[test]
    fn rejects_non_origin_input() {
        assert_eq!(https(""), Err(OriginError::Empty));
        assert_eq!(https("   "), Err(OriginError::Empty));
        assert_eq!(https("https://"), Err(OriginError::Invalid));
        assert_eq!(https("https://exa mple.com"), Err(OriginError::Invalid));
        assert_eq!(
            https("https://user:pw@portal.example.com"),
            Err(OriginError::CredentialsNotAllowed)
        );
        assert_eq!(
            https("https://user@portal.example.com"),
            Err(OriginError::CredentialsNotAllowed)
        );
        assert_eq!(
            https("https://portal.example.com/sites"),
            Err(OriginError::NotAnOrigin)
        );
        assert_eq!(
            https("https://portal.example.com/?a=1"),
            Err(OriginError::NotAnOrigin)
        );
        assert_eq!(
            https("https://portal.example.com/#top"),
            Err(OriginError::NotAnOrigin)
        );
        assert_eq!(
            https("ftp://portal.example.com"),
            Err(OriginError::UnsupportedScheme)
        );
        assert_eq!(
            https("file:///etc/passwd"),
            Err(OriginError::UnsupportedScheme)
        );
    }

    // @spec CLIENT-009
    #[test]
    fn requires_https_without_development_policy() {
        assert_eq!(
            https("http://portal.example.com"),
            Err(OriginError::InsecureScheme)
        );
        assert_eq!(
            https("http://localhost:5000"),
            Err(OriginError::InsecureScheme)
        );
        assert_eq!(
            https("http://127.0.0.1:5000"),
            Err(OriginError::InsecureScheme)
        );
    }

    // @spec CLIENT-009
    #[test]
    fn development_policy_allows_http_only_for_loopback() {
        assert_eq!(
            dev("http://localhost:5000"),
            Ok("http://localhost:5000".into())
        );
        assert_eq!(
            dev("http://127.0.0.2:5000"),
            Ok("http://127.0.0.2:5000".into())
        );
        assert_eq!(dev("http://[::1]:5000"), Ok("http://[::1]:5000".into()));
        assert_eq!(
            dev("http://portal.example.com"),
            Err(OriginError::InsecureScheme)
        );
        assert_eq!(
            dev("http://localhost.example.com"),
            Err(OriginError::InsecureScheme)
        );
        assert_eq!(dev("http://192.168.1.10"), Err(OriginError::InsecureScheme));
        assert_eq!(dev("localhost:5000"), Ok("https://localhost:5000".into()));
    }
}
