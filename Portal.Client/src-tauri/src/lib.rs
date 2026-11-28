use portal_onboarding::{OriginPolicy, PROBE_TIMEOUT, PendingOrigin, PortalOrigin, ProbeOutcome};
use portal_session::{
    CredentialStore, ExternalKind, KeyringCredentialStore, MemoryCredentialStore, SIGN_IN_TIMEOUT,
    SessionManager, SessionView, SignInError,
};
use serde::Serialize;
use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use tauri::{AppHandle, Manager, State};
use tauri_plugin_opener::OpenerExt;
use tokio::sync::oneshot;
#[cfg(target_os = "android")]
use url::Url;

const CREDENTIAL_SERVICE: &str = "app.swick.portal.client";
const CREDENTIAL_USER: &str = "session";

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
    portal_version: Option<String>,
}

fn config_path(app: &AppHandle) -> Result<PathBuf, String> {
    app.path()
        .app_config_dir()
        .map(|dir| dir.join("portal.json"))
        .map_err(|error| format!("Could not locate the configuration directory: {error}"))
}

/// @spec CLIENT-008 CLIENT-009 CLIENT-010 CLIENT-011 CLIENT-012 CLIENT-023 CLIENT-024 CLIENT-025 CLIENT-026 CLIENT-031
#[tauri::command]
async fn check_portal_url(
    input: String,
    pending: State<'_, PendingOrigin>,
) -> Result<CheckResult, String> {
    let generation = pending.begin_check();
    let origin = PortalOrigin::parse(&input, ORIGIN_POLICY).map_err(|error| error.to_string())?;
    let client =
        portal_onboarding::probe_client(PROBE_TIMEOUT).map_err(|error| error.to_string())?;
    let checked = portal_onboarding::check_portal(&client, &origin, ORIGIN_POLICY).await;
    if checked.outcome.found_portal() {
        pending.publish_if_current(generation, checked.origin.clone());
    }
    Ok(CheckResult {
        origin: checked.origin.to_string(),
        outcome: checked.outcome,
        portal_version: checked.portal_version,
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

fn saved_origin(app: &AppHandle) -> Result<PortalOrigin, String> {
    portal_onboarding::load_origin(&config_path(app)?, ORIGIN_POLICY)
        .map_err(|error| error.to_string())?
        .ok_or_else(|| "Choose your Digital Fortress address first.".to_owned())
}

/// The in-flight sign-in: cancelling it, or (on Android) delivering the deep-link callback.
#[derive(Default)]
struct SignInSlot {
    cancel: Mutex<Option<oneshot::Sender<()>>>,
    #[cfg(target_os = "android")]
    callback: Mutex<Option<oneshot::Sender<Url>>>,
}

/// Selects the platform credential store; without one the session is kept in memory only.
/// @spec CLIENT-022 CLIENT-038
fn credential_store() -> Arc<dyn CredentialStore> {
    #[cfg(target_os = "linux")]
    let store = zbus_secret_service_keyring_store::Store::new().map(|s| s as Arc<keyring_core::CredentialStore>);
    #[cfg(target_os = "windows")]
    let store = windows_native_keyring_store::Store::new().map(|s| s as Arc<keyring_core::CredentialStore>);
    #[cfg(target_os = "android")]
    let store = android_native_keyring_store::Store::new().map(|s| s as Arc<keyring_core::CredentialStore>);
    #[cfg(not(any(target_os = "linux", target_os = "windows", target_os = "android")))]
    let store: keyring_core::Result<Arc<keyring_core::CredentialStore>> =
        Err(keyring_core::Error::NotSupportedByStore("platform".into()));

    store
        .ok()
        .and_then(|store| {
            keyring_core::set_default_store(store);
            KeyringCredentialStore::new(CREDENTIAL_SERVICE, CREDENTIAL_USER).ok()
        })
        .map(|store| Arc::new(store) as Arc<dyn CredentialStore>)
        .unwrap_or_else(|| Arc::new(MemoryCredentialStore::unavailable()))
}

/// @spec CLIENT-018 CLIENT-039
#[tauri::command]
async fn session_startup(app: AppHandle, session: State<'_, SessionManager>) -> Result<SessionView, String> {
    Ok(session.startup(&saved_origin(&app)?).await)
}

/// @spec CLIENT-016 CLIENT-018 CLIENT-019 CLIENT-020 CLIENT-040
#[tauri::command]
async fn refresh_manifest(app: AppHandle, session: State<'_, SessionManager>) -> Result<SessionView, String> {
    Ok(session.refresh(&saved_origin(&app)?).await)
}

/// Opens the system browser and waits for the redirect, cancellation, or timeout.
/// @spec CLIENT-014 CLIENT-015 CLIENT-034 CLIENT-035 CLIENT-036
#[tauri::command]
async fn sign_in(
    app: AppHandle,
    session: State<'_, SessionManager>,
    slot: State<'_, SignInSlot>,
) -> Result<SessionView, String> {
    let origin = saved_origin(&app)?;
    let (cancel_tx, cancel_rx) = oneshot::channel();
    *slot.cancel.lock().unwrap() = Some(cancel_tx);

    #[cfg(not(target_os = "android"))]
    let (redirect, callback) = {
        let listener = portal_session::CallbackListener::bind().await.map_err(|e| e.to_string())?;
        (portal_session::DESKTOP_REDIRECT_URI, async move { Some(listener.accept_callback().await) })
    };
    #[cfg(target_os = "android")]
    let (redirect, callback) = {
        let (tx, rx) = oneshot::channel();
        *slot.callback.lock().unwrap() = Some(tx);
        (portal_session::ANDROID_REDIRECT_URI, async move { rx.await.ok() })
    };

    let url = session.begin_sign_in(&origin, redirect).await.map_err(|e| e.to_string())?;
    if let Err(error) = app.opener().open_url(url.as_str(), None::<&str>) {
        session.cancel_sign_in().await;
        return Err(format!("Could not open the system browser: {error}"));
    }

    let outcome = tokio::select! {
        result = async {
            let callback = callback.await.ok_or(SignInError::Cancelled)?;
            session.complete_sign_in(&origin, &callback).await
        } => result,
        _ = cancel_rx => Err(SignInError::Cancelled),
        _ = tokio::time::sleep(SIGN_IN_TIMEOUT) => Err(SignInError::Expired),
    };
    slot.cancel.lock().unwrap().take();
    match outcome {
        Ok(view) => Ok(view),
        Err(error) => {
            session.cancel_sign_in().await;
            Err(error.to_string())
        }
    }
}

/// @spec CLIENT-036
#[tauri::command]
async fn cancel_sign_in(session: State<'_, SessionManager>, slot: State<'_, SignInSlot>) -> Result<(), String> {
    if let Some(cancel) = slot.cancel.lock().unwrap().take() {
        let _ = cancel.send(());
    }
    session.cancel_sign_in().await;
    Ok(())
}

/// @spec CLIENT-021
#[tauri::command]
async fn sign_out(session: State<'_, SessionManager>) -> Result<SessionView, String> {
    Ok(session.sign_out().await)
}

/// @spec CLIENT-044 CLIENT-045 CLIENT-046
#[tauri::command]
fn open_external(app: AppHandle, url: String, kind: ExternalKind) -> Result<(), String> {
    let url = portal_session::validate_external(&url, kind).ok_or("This link can't be opened.")?;
    app.opener().open_url(url.as_str(), None::<&str>).map_err(|error| error.to_string())
}

/// @spec CLIENT-045
#[tauri::command]
fn platform() -> &'static str {
    std::env::consts::OS
}

/// @spec CLIENT-030
#[tauri::command]
fn app_version(app: AppHandle) -> String {
    app.package_info().version.to_string()
}

/// Probes the saved origin without discovery; failures yield no version.
/// @spec CLIENT-033
#[tauri::command]
async fn saved_portal_version(app: AppHandle) -> Result<Option<String>, String> {
    let Some(origin) = portal_onboarding::load_origin(&config_path(&app)?, ORIGIN_POLICY)
        .map_err(|error| error.to_string())?
    else {
        return Ok(None);
    };
    let client =
        portal_onboarding::probe_client(PROBE_TIMEOUT).map_err(|error| error.to_string())?;
    let (outcome, version) = portal_onboarding::probe_portal(&client, &origin).await;
    Ok(version.filter(|_| outcome.found_portal()))
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    let builder = tauri::Builder::default().plugin(tauri_plugin_opener::init());
    #[cfg(target_os = "android")]
    let builder = builder.plugin(tauri_plugin_deep_link::init());
    builder
        .manage(PendingOrigin::default())
        .manage(SignInSlot::default())
        .setup(|app| {
            let data_dir = app.path().app_data_dir()?;
            let http = portal_session::http_client()?;
            app.manage(SessionManager::new(http, ORIGIN_POLICY, data_dir, credential_store()));

            // @spec CLIENT-035
            #[cfg(target_os = "android")]
            {
                use tauri_plugin_deep_link::DeepLinkExt;
                let handle = app.handle().clone();
                app.deep_link().on_open_url(move |event| {
                    let expected = Url::parse(portal_session::ANDROID_REDIRECT_URI).ok();
                    for url in event.urls() {
                        let matches = expected.as_ref().is_some_and(|expected| {
                            url.scheme() == expected.scheme() && url.path() == expected.path()
                        });
                        if matches {
                            if let Some(tx) = handle.state::<SignInSlot>().callback.lock().unwrap().take() {
                                let _ = tx.send(url);
                            }
                        }
                    }
                });
            }
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            check_portal_url,
            save_portal_url,
            saved_portal_url,
            session_startup,
            refresh_manifest,
            sign_in,
            cancel_sign_in,
            sign_out,
            open_external,
            platform,
            app_version,
            saved_portal_version
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
