use std::fs;
use std::io;
use std::path::Path;

use serde::{Deserialize, Serialize};

use crate::{OriginError, OriginPolicy, PortalOrigin};

/// The persisted configuration holds only the origin; no trust or credentials.
#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct SavedPortal {
    origin: String,
}

#[derive(Debug)]
pub enum StoreError {
    Io(io::Error),
    Corrupt,
    Origin(OriginError),
}

// @spec CLIENT-029
impl std::fmt::Display for StoreError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::Io(error) => write!(
                f,
                "Could not access the saved Digital Fortress address: {error}"
            ),
            Self::Corrupt => f.write_str("The saved Digital Fortress address file is not valid."),
            Self::Origin(error) => write!(
                f,
                "The saved Digital Fortress address is not valid: {error}"
            ),
        }
    }
}

impl std::error::Error for StoreError {}

/// @spec CLIENT-013
pub fn save_origin(path: &Path, origin: &PortalOrigin) -> Result<(), StoreError> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).map_err(StoreError::Io)?;
    }
    let json = serde_json::to_vec_pretty(&SavedPortal {
        origin: origin.to_string(),
    })
    .expect("serializing a string field cannot fail");
    let temporary = path.with_extension("json.tmp");
    fs::write(&temporary, json).map_err(StoreError::Io)?;
    fs::rename(&temporary, path).map_err(StoreError::Io)
}

/// @spec CLIENT-013
pub fn load_origin(path: &Path, policy: OriginPolicy) -> Result<Option<PortalOrigin>, StoreError> {
    let bytes = match fs::read(path) {
        Ok(bytes) => bytes,
        Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
        Err(error) => return Err(StoreError::Io(error)),
    };
    let saved: SavedPortal = serde_json::from_slice(&bytes).map_err(|_| StoreError::Corrupt)?;
    PortalOrigin::parse(&saved.origin, policy)
        .map(Some)
        .map_err(StoreError::Origin)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn temp_dir(name: &str) -> std::path::PathBuf {
        let dir =
            std::env::temp_dir().join(format!("portal-onboarding-{name}-{}", std::process::id()));
        let _ = fs::remove_dir_all(&dir);
        dir
    }

    // @spec CLIENT-013
    #[test]
    fn saves_only_the_origin_and_round_trips() {
        let dir = temp_dir("roundtrip");
        let path = dir.join("portal.json");
        assert!(
            load_origin(&path, OriginPolicy::HttpsOnly)
                .unwrap()
                .is_none()
        );

        let origin = PortalOrigin::parse("portal.example.com", OriginPolicy::HttpsOnly).unwrap();
        save_origin(&path, &origin).unwrap();

        let saved: serde_json::Value = serde_json::from_slice(&fs::read(&path).unwrap()).unwrap();
        assert_eq!(
            saved,
            serde_json::json!({ "origin": "https://portal.example.com" })
        );
        assert_eq!(
            load_origin(&path, OriginPolicy::HttpsOnly).unwrap(),
            Some(origin)
        );
        fs::remove_dir_all(dir).unwrap();
    }

    // @spec CLIENT-013 CLIENT-029
    #[test]
    fn revalidates_saved_origin_on_load() {
        let dir = temp_dir("revalidate");
        fs::create_dir_all(&dir).unwrap();
        let path = dir.join("portal.json");

        fs::write(&path, r#"{ "origin": "http://localhost:5000" }"#).unwrap();
        let error = load_origin(&path, OriginPolicy::HttpsOnly).unwrap_err();
        assert!(matches!(
            &error,
            StoreError::Origin(OriginError::InsecureScheme)
        ));
        assert!(error.to_string().contains("Digital Fortress"));
        assert!(error.to_string().contains("https"));

        fs::write(
            &path,
            r#"{ "origin": "https://portal.example.com", "trusted": true }"#,
        )
        .unwrap();
        let error = load_origin(&path, OriginPolicy::HttpsOnly).unwrap_err();
        assert!(matches!(&error, StoreError::Corrupt));
        assert!(error.to_string().contains("Digital Fortress"));
        fs::remove_dir_all(dir).unwrap();
    }
}
