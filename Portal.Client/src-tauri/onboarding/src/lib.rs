//! Manual Portal URL onboarding for the native client (issue #15).

mod origin;
mod pending;
mod probe;
mod store;

pub use origin::{OriginError, OriginPolicy, PortalOrigin};
pub use pending::PendingOrigin;
pub use probe::{
    PROBE_PATH, PROBE_TIMEOUT, PortalCheck, ProbeOutcome, check_portal, probe, probe_client,
    probe_portal,
};
pub use store::{StoreError, load_origin, save_origin};
