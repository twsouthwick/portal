use portal_onboarding::{OriginPolicy, PROBE_TIMEOUT, PendingOrigin, PortalOrigin, ProbeOutcome};
use serde::Serialize;
use std::path::PathBuf;
use tauri::{AppHandle, Manager, State};

const ORIGIN_POLICY: OriginPolicy = if cfg!(all(debug_assertions, feature = "dev-http-loopback")) {
    OriginPolicy::AllowHttpLoopback
} else {
    OriginPolicy::HttpsOnly
};

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct CheckResult {
    origin: String,
    outcome: ProbeOutcome,
}

fn config_path(app: &AppHandle) -> Result<PathBuf, String> {
    app.path()
        .app_config_dir()
        .map(|dir| dir.join("portal.json"))
        .map_err(|error| format!("Could not locate the configuration directory: {error}"))
}

/// @spec CLIENT-008 CLIENT-009 CLIENT-010 CLIENT-011 CLIENT-012 CLIENT-023 CLIENT-024 CLIENT-025 CLIENT-026
#[tauri::command]
async fn check_portal_url(
    input: String,
    pending: State<'_, PendingOrigin>,
) -> Result<CheckResult, String> {
    let generation = pending.begin_check();
    let origin = PortalOrigin::parse(&input, ORIGIN_POLICY).map_err(|error| error.to_string())?;
    let client =
        portal_onboarding::probe_client(PROBE_TIMEOUT).map_err(|error| error.to_string())?;
    let (origin, outcome) = portal_onboarding::check_portal(&client, &origin, ORIGIN_POLICY).await;
    if outcome.found_portal() {
        pending.publish_if_current(generation, origin.clone());
    }
    Ok(CheckResult {
        origin: origin.to_string(),
        outcome,
    })
}

/// @spec CLIENT-013
#[tauri::command]
fn save_portal_url(
    origin: String,
    app: AppHandle,
    pending: State<'_, PendingOrigin>,
) -> Result<String, String> {
    let confirmed = pending.save_if_confirmed(&config_path(&app)?, &origin)?;
    Ok(confirmed.to_string())
}

/// @spec CLIENT-013
#[tauri::command]
fn saved_portal_url(app: AppHandle) -> Result<Option<String>, String> {
    portal_onboarding::load_origin(&config_path(&app)?, ORIGIN_POLICY)
        .map(|origin| origin.map(|origin| origin.to_string()))
        .map_err(|error| error.to_string())
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .manage(PendingOrigin::default())
        .invoke_handler(tauri::generate_handler![
            check_portal_url,
            save_portal_url,
            saved_portal_url
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
