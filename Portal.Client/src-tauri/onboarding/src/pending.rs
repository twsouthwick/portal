use std::path::Path;
use std::sync::Mutex;

use crate::{PortalOrigin, save_origin};

#[derive(Default)]
pub struct PendingOrigin(Mutex<PendingState>);

#[derive(Default)]
struct PendingState {
    generation: u64,
    origin: Option<PortalOrigin>,
}

impl PendingOrigin {
    pub fn begin_check(&self) -> u64 {
        let mut pending = self.0.lock().unwrap();
        pending.generation = pending.generation.wrapping_add(1);
        pending.origin = None;
        pending.generation
    }

    pub fn publish_if_current(&self, generation: u64, origin: PortalOrigin) -> bool {
        let mut pending = self.0.lock().unwrap();
        if pending.generation != generation {
            return false;
        }

        pending.origin = Some(origin);
        true
    }

    /// @spec CLIENT-013 CLIENT-029
    pub fn save_if_confirmed(
        &self,
        path: &Path,
        expected_origin: &str,
    ) -> Result<PortalOrigin, String> {
        let mut pending = self.0.lock().unwrap();
        let confirmed = match pending.origin.as_ref() {
            Some(checked) if checked.as_str() == expected_origin => checked.clone(),
            _ => return Err("Check this Digital Fortress address before saving it.".into()),
        };
        save_origin(path, &confirmed).map_err(|error| error.to_string())?;
        pending.origin = None;
        Ok(confirmed)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::OriginPolicy;

    fn origin(value: &str) -> PortalOrigin {
        PortalOrigin::parse(value, OriginPolicy::HttpsOnly).unwrap()
    }

    // @spec CLIENT-013
    #[test]
    fn stale_check_cannot_restore_pending_origin_after_newer_check_fails() {
        let pending = PendingOrigin::default();
        let stale_generation = pending.begin_check();
        let _current_generation = pending.begin_check();

        assert!(!pending.publish_if_current(stale_generation, origin("https://first.example.com")));
        assert!(pending.0.lock().unwrap().origin.is_none());
    }

    // @spec CLIENT-013
    #[test]
    fn stale_check_cannot_replace_the_latest_pending_origin() {
        let pending = PendingOrigin::default();
        let stale_generation = pending.begin_check();
        let current_generation = pending.begin_check();
        let current_origin = origin("https://second.example.com");

        assert!(pending.publish_if_current(current_generation, current_origin.clone()));
        assert!(!pending.publish_if_current(stale_generation, origin("https://first.example.com")));
        assert_eq!(pending.0.lock().unwrap().origin, Some(current_origin));
    }

    // @spec CLIENT-013 CLIENT-029
    #[test]
    fn unchecked_address_has_branded_confirmation_guidance() {
        let pending = PendingOrigin::default();
        let error = pending
            .save_if_confirmed(Path::new("unused"), "https://example.com")
            .unwrap_err();
        assert!(error.contains("Digital Fortress"));
        assert!(error.contains("Check"));
    }
}
