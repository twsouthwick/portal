use std::path::PathBuf;
use std::sync::Mutex;

use portal_onboarding::{OriginPolicy, PROBE_TIMEOUT, PortalOrigin, ProbeOutcome};
use serde::Serialize;
use tauri::{AppHandle, Manager, State};

const ORIGIN_POLICY: OriginPolicy = if cfg!(all(debug_assertions, feature = "dev-http-loopback")) {
    OriginPolicy::AllowHttpLoopback
} else {
    OriginPolicy::HttpsOnly
};

/// The origin most recently checked successfully, awaiting user confirmation.
#[derive(Default)]
struct PendingOrigin(Mutex<Option<PortalOrigin>>);

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

/// @spec CLIENT-008 CLIENT-009 CLIENT-010 CLIENT-011 CLIENT-012
#[tauri::command]
async fn check_portal_url(
    input: String,
    pending: State<'_, PendingOrigin>,
) -> Result<CheckResult, String> {
    *pending.0.lock().unwrap() = None;
    let origin = PortalOrigin::parse(&input, ORIGIN_POLICY).map_err(|error| error.to_string())?;
    let client =
        portal_onboarding::probe_client(PROBE_TIMEOUT).map_err(|error| error.to_string())?;
    let outcome = portal_onboarding::probe(&client, &origin).await;
    if outcome.found_portal() {
        *pending.0.lock().unwrap() = Some(origin.clone());
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
    let mut pending = pending.0.lock().unwrap();
    let confirmed = match pending.as_ref() {
        Some(checked) if checked.as_str() == origin => checked.clone(),
        _ => return Err("Check this Portal URL before saving it.".into()),
    };
    portal_onboarding::save_origin(&config_path(&app)?, &confirmed)
        .map_err(|error| error.to_string())?;
    *pending = None;
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
