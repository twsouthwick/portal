use std::fs;
use std::io::Write;
use std::path::{Path, PathBuf};

use chacha20poly1305::aead::{Aead, KeyInit, Payload};
use chacha20poly1305::{XChaCha20Poly1305, XNonce};
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};

use crate::credentials::{Account, CacheKey};
use crate::manifest::Manifest;

const NONCE_LEN: usize = 24;
pub const CACHE_DIR: &str = "manifest-cache";

/// Decrypted cache contents for one `(origin, iss, sub)` account.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CachedManifest {
    pub account: Account,
    pub etag: Option<String>,
    /// Unix seconds of the last successful refresh.
    pub refreshed_at: u64,
    pub manifest: Manifest,
}

#[derive(Debug, thiserror::Error)]
pub enum CacheError {
    #[error("Could not save the Digital Fortress site list: {0}")]
    Io(#[from] std::io::Error),
    #[error("Could not protect the Digital Fortress site list.")]
    Crypto,
}

/// @spec CLIENT-039
pub fn cache_file_name(account: &Account) -> String {
    let mut hasher = Sha256::new();
    for part in [&account.origin, &account.issuer, &account.subject] {
        hasher.update((part.len() as u64).to_be_bytes());
        hasher.update(part.as_bytes());
    }
    format!("{}.bin", hex::encode(hasher.finalize()))
}

pub fn cache_path(data_dir: &Path, account: &Account) -> PathBuf {
    data_dir.join(CACHE_DIR).join(cache_file_name(account))
}

/// @spec CLIENT-017 CLIENT-022 CLIENT-039
pub fn write_cache(data_dir: &Path, key: &CacheKey, cached: &CachedManifest) -> Result<(), CacheError> {
    write_encrypted(data_dir, key, &cached.account, cached)
}

fn write_encrypted(
    data_dir: &Path,
    key: &CacheKey,
    file_account: &Account,
    cached: &CachedManifest,
) -> Result<(), CacheError> {
    let path = cache_path(data_dir, file_account);
    let name = cache_file_name(file_account);
    let plaintext = serde_json::to_vec(cached).map_err(|_| CacheError::Crypto)?;
    let mut nonce = [0u8; NONCE_LEN];
    getrandom::fill(&mut nonce).map_err(|_| CacheError::Crypto)?;
    let cipher = XChaCha20Poly1305::new(key.as_bytes().into());
    let ciphertext = cipher
        .encrypt(
            &XNonce::from(nonce),
            Payload { msg: &plaintext, aad: name.as_bytes() },
        )
        .map_err(|_| CacheError::Crypto)?;

    let dir = path.parent().expect("cache path has a parent");
    fs::create_dir_all(dir)?;
    let temp = dir.join(format!("{name}.tmp"));
    {
        let mut file = fs::File::create(&temp)?;
        file.write_all(&nonce)?;
        file.write_all(&ciphertext)?;
        file.sync_all()?;
    }
    fs::rename(&temp, &path)?;
    Ok(())
}

/// Returns the cache only if it decrypts under `key` and belongs to `account`.
/// @spec CLIENT-018 CLIENT-039
pub fn read_cache(data_dir: &Path, key: &CacheKey, account: &Account) -> Option<CachedManifest> {
    let name = cache_file_name(account);
    let bytes = fs::read(cache_path(data_dir, account)).ok()?;
    if bytes.len() < NONCE_LEN {
        return None;
    }
    let (nonce, ciphertext) = bytes.split_at(NONCE_LEN);
    let cipher = XChaCha20Poly1305::new(key.as_bytes().into());
    let plaintext = cipher
        .decrypt(&XNonce::try_from(nonce).ok()?, Payload { msg: ciphertext, aad: name.as_bytes() })
        .ok()?;
    let cached: CachedManifest = serde_json::from_slice(&plaintext).ok()?;
    (cached.account == *account).then_some(cached)
}

/// @spec CLIENT-021
pub fn delete_cache(data_dir: &Path, account: &Account) -> Result<(), CacheError> {
    match fs::remove_file(cache_path(data_dir, account)) {
        Err(error) if error.kind() != std::io::ErrorKind::NotFound => Err(error.into()),
        _ => Ok(()),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::manifest::Manifest;

    fn account(subject: &str) -> Account {
        Account {
            origin: "https://portal.example.com".into(),
            issuer: "https://id.example.com".into(),
            subject: subject.into(),
        }
    }

    fn cached(account: Account) -> CachedManifest {
        CachedManifest {
            account,
            etag: Some("\"abc\"".into()),
            refreshed_at: 42,
            manifest: Manifest {
                schema_version: 1,
                name: "Home".into(),
                description: "Sites".into(),
                sites: vec![],
            },
        }
    }

    // @spec CLIENT-017 CLIENT-039
    #[test]
    fn round_trips_encrypted_cache_for_the_same_account() {
        let dir = tempfile::tempdir().unwrap();
        let key = CacheKey::generate().unwrap();
        let entry = cached(account("alice"));
        write_cache(dir.path(), &key, &entry).unwrap();

        let raw = fs::read(cache_path(dir.path(), &entry.account)).unwrap();
        assert!(!String::from_utf8_lossy(&raw).contains("Home"));
        assert_eq!(read_cache(dir.path(), &key, &entry.account), Some(entry));
        assert!(!dir.path().join(CACHE_DIR).read_dir().unwrap().any(|file| {
            file.unwrap().path().extension().is_some_and(|ext| ext == "tmp")
        }));
    }

    // @spec CLIENT-018 CLIENT-021 CLIENT-039
    #[test]
    fn does_not_display_cache_with_another_key_or_account() {
        let dir = tempfile::tempdir().unwrap();
        let key = CacheKey::generate().unwrap();
        let alice = cached(account("alice"));
        write_cache(dir.path(), &key, &alice).unwrap();

        assert_eq!(read_cache(dir.path(), &CacheKey::generate().unwrap(), &alice.account), None);
        assert_eq!(read_cache(dir.path(), &key, &account("bob")), None);

        // A file copied to another account's name fails authentication.
        fs::copy(
            cache_path(dir.path(), &alice.account),
            cache_path(dir.path(), &account("bob")),
        )
        .unwrap();
        assert_eq!(read_cache(dir.path(), &key, &account("bob")), None);

        // Authentic ciphertext whose contents name another account is rejected.
        write_encrypted(dir.path(), &key, &account("carol"), &alice).unwrap();
        assert_eq!(read_cache(dir.path(), &key, &account("carol")), None);

        delete_cache(dir.path(), &alice.account).unwrap();
        assert_eq!(read_cache(dir.path(), &key, &alice.account), None);
        delete_cache(dir.path(), &alice.account).unwrap();
    }

    // @spec CLIENT-039
    #[test]
    fn scopes_file_name_by_origin_issuer_and_subject() {
        let base = account("alice");
        let mut other_origin = base.clone();
        other_origin.origin = "https://other.example.com".into();
        assert_ne!(cache_file_name(&base), cache_file_name(&other_origin));
        assert_ne!(cache_file_name(&base), cache_file_name(&account("alice2")));
    }
}
