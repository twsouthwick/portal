use std::collections::HashMap;
use std::fmt;
use std::sync::Mutex;

use base64::Engine;
use base64::engine::general_purpose::STANDARD;
use serde::{Deserialize, Deserializer, Serialize, Serializer};

/// The authenticated account scope: Portal origin plus validated `iss` and `sub`.
#[derive(Clone, Debug, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Account {
    pub origin: String,
    pub issuer: String,
    pub subject: String,
}

/// A random 256-bit key that encrypts one account's manifest cache.
#[derive(Clone, PartialEq, Eq)]
pub struct CacheKey([u8; 32]);

impl CacheKey {
    pub fn generate() -> Result<Self, getrandom::Error> {
        let mut key = [0u8; 32];
        getrandom::fill(&mut key)?;
        Ok(Self(key))
    }

    pub fn as_bytes(&self) -> &[u8; 32] {
        &self.0
    }
}

impl fmt::Debug for CacheKey {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str("CacheKey(..)")
    }
}

impl Serialize for CacheKey {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        serializer.serialize_str(&STANDARD.encode(self.0))
    }
}

impl<'de> Deserialize<'de> for CacheKey {
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        let encoded = String::deserialize(deserializer)?;
        let bytes = STANDARD.decode(encoded).map_err(serde::de::Error::custom)?;
        let key: [u8; 32] = bytes
            .try_into()
            .map_err(|_| serde::de::Error::custom("cache key must be 32 bytes"))?;
        Ok(Self(key))
    }
}

/// The single credential-store entry for a signed-in account.
#[derive(Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct StoredSession {
    pub account: Account,
    pub refresh_token: Option<String>,
    pub cache_key: CacheKey,
}

impl fmt::Debug for StoredSession {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_struct("StoredSession")
            .field("account", &self.account)
            .field("refresh_token", &self.refresh_token.as_ref().map(|_| ".."))
            .finish_non_exhaustive()
    }
}

#[derive(Debug, thiserror::Error)]
#[error("{0}")]
pub struct CredentialError(pub String);

/// Platform credential store holding at most one [`StoredSession`].
/// @spec CLIENT-022 CLIENT-034
pub trait CredentialStore: Send + Sync {
    fn load(&self) -> Result<Option<StoredSession>, CredentialError>;
    fn save(&self, session: &StoredSession) -> Result<(), CredentialError>;
    fn delete(&self) -> Result<(), CredentialError>;
}

/// In-memory store used by tests.
#[derive(Default)]
pub struct MemoryCredentialStore {
    entries: Mutex<HashMap<(), StoredSession>>,
    pub unavailable: bool,
}

impl MemoryCredentialStore {
    pub fn unavailable() -> Self {
        Self { unavailable: true, ..Self::default() }
    }

    fn check(&self) -> Result<(), CredentialError> {
        if self.unavailable {
            Err(CredentialError("Secure storage is unavailable.".into()))
        } else {
            Ok(())
        }
    }
}

impl CredentialStore for MemoryCredentialStore {
    fn load(&self) -> Result<Option<StoredSession>, CredentialError> {
        self.check()?;
        Ok(self.entries.lock().unwrap().get(&()).cloned())
    }

    fn save(&self, session: &StoredSession) -> Result<(), CredentialError> {
        self.check()?;
        self.entries.lock().unwrap().insert((), session.clone());
        Ok(())
    }

    fn delete(&self) -> Result<(), CredentialError> {
        self.check()?;
        self.entries.lock().unwrap().clear();
        Ok(())
    }
}

/// Stores the session as one JSON secret in the platform credential store
/// selected by `keyring_core::set_default_store`.
pub struct KeyringCredentialStore {
    entry: keyring_core::Entry,
}

impl KeyringCredentialStore {
    pub fn new(service: &str, user: &str) -> Result<Self, CredentialError> {
        keyring_core::Entry::new(service, user)
            .map(|entry| Self { entry })
            .map_err(|error| CredentialError(error.to_string()))
    }
}

impl CredentialStore for KeyringCredentialStore {
    fn load(&self) -> Result<Option<StoredSession>, CredentialError> {
        match self.entry.get_password() {
            // An unreadable entry is treated as signed out.
            Ok(secret) => Ok(serde_json::from_str(&secret).ok()),
            Err(keyring_core::Error::NoEntry) => Ok(None),
            Err(error) => Err(CredentialError(error.to_string())),
        }
    }

    fn save(&self, session: &StoredSession) -> Result<(), CredentialError> {
        let secret = serde_json::to_string(session).map_err(|error| CredentialError(error.to_string()))?;
        self.entry.set_password(&secret).map_err(|error| CredentialError(error.to_string()))
    }

    fn delete(&self) -> Result<(), CredentialError> {
        match self.entry.delete_credential() {
            Ok(()) | Err(keyring_core::Error::NoEntry) => Ok(()),
            Err(error) => Err(CredentialError(error.to_string())),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // @spec CLIENT-022 CLIENT-034
    #[test]
    fn keyring_store_keeps_one_entry() {
        keyring_core::set_default_store(keyring_core::mock::Store::new().unwrap());
        let store = KeyringCredentialStore::new("app.swick.portal.client", "session").unwrap();
        assert!(store.load().unwrap().is_none());
        store.delete().unwrap();
        let session = StoredSession {
            account: Account { origin: "https://p".into(), issuer: "https://i".into(), subject: "s".into() },
            refresh_token: Some("r".into()),
            cache_key: CacheKey::generate().unwrap(),
        };
        store.save(&session).unwrap();
        assert_eq!(store.load().unwrap(), Some(session));
        store.delete().unwrap();
        assert!(store.load().unwrap().is_none());
    }

    // @spec CLIENT-034
    #[test]
    fn stored_session_round_trips_without_debug_secrets() {
        let session = StoredSession {
            account: Account {
                origin: "https://portal.example.com".into(),
                issuer: "https://id.example.com".into(),
                subject: "alice".into(),
            },
            refresh_token: Some("refresh-secret".into()),
            cache_key: CacheKey::generate().unwrap(),
        };
        let json = serde_json::to_string(&session).unwrap();
        assert_eq!(serde_json::from_str::<StoredSession>(&json).unwrap(), session);
        let debug = format!("{session:?}");
        assert!(!debug.contains("refresh-secret"));
    }
}
