# Developer Guide

For Portal features, configuration, and deployment, see the [user guide](README.md).
This document covers local development, implementation boundaries, builds, tests,
the native client, and CI.

## Local setup

The repository pins the .NET SDK in [`global.json`](global.json) (currently
.NET 11.0.100). The web frontend and native client use Node.js; CI uses Node.js
24. Install the Aspire CLI 17.0.0 to run the local application topology.

The VS Code devcontainer installs the pinned SDK, Aspire CLI, Node.js 24, and
Docker-in-Docker. Reopen the repository in the devcontainer, then run:

```bash
aspire run
```

The AppHost starts Keycloak at `http://localhost:8080`, imports the development
realm, and launches the `media` and `admin` web profiles. Use either development
account:

| Username | Password | Groups |
| --- | --- | --- |
| `admin` | `admin` | `admin`, `media` |
| `media` | `media` | `media` |

These credentials and the local OIDC client secret are development-only.

## Build and test

From the repository root, install the web dependencies, restore, build, and run
the .NET tests:

```bash
npm ci --prefix Portal.Web
dotnet restore Portal.slnx
dotnet build Portal.slnx
dotnet test Portal.slnx
```

`Portal.Web/Portal.Web.csproj` invokes the matching npm build before a .NET
build, and installs npm dependencies if `node_modules` is absent. Debug builds
use the frontend development build; non-Debug builds use the production build.
The .NET tests are in `Portal.Web.Tests`.

The native client has its own end-to-end tests, documented under
[Native client](#native-client). CI runs the .NET tests, Helm chart lint, and
native client smoke/UI checks. See [CI and contribution checks](#ci-and-contribution-checks)
for the current workflow coverage.

## Frontend development

Frontend sources are in `Portal.Web/ts` and `Portal.Web/css`. Webpack compiles
and bundles them into `Portal.Web/wwwroot/dist`, which is generated and
gitignored.

```bash
cd Portal.Web
npm ci
npm run build   # type-check and create a production bundle
npm run dev     # type-check and create a development bundle
npm run watch   # watch TypeScript and bundle changes
npm run clean   # remove generated output
```

`IClientBrowserSession` is the browser-interoperability boundary used by Razor
components. TypeScript helpers include private-mode detection, app-protocol
launching, and opening URLs in a new tab; use the service rather than invoking
the bundled JavaScript directly.

## Project boundaries

- `Portal.AppHost` owns Aspire resource and local profile wiring.
- `Portal.ServiceDefaults` owns shared Aspire defaults such as telemetry,
  health checks, service discovery, and HTTP resilience.
- `Portal.Web` is the Blazor Interactive Server application. `Program.cs` is
  its composition root.
- `Portal.Web/Services` owns portal/site loading, Markdown conversion, and
  click tracking. Sites may come from a directory or ZIP selected by
  `SITE_PATH`.
- `Portal.Web/Browser` owns browser and JavaScript interop.
- `Portal.Web/ts` and `Portal.Web/css` are frontend sources; generated bundles
  belong in `Portal.Web/wwwroot/dist`.
- `Portal.Web.Tests` contains the web application tests.
- `Portal.Client` is a standalone Tauri v2 shell. It currently displays a
  placeholder and does not connect to the portal or implement authentication.

## Native client

The `Portal.Client` Tauri v2 shell is a scaffold; it does not yet connect to the
portal, implement authentication, or change `Portal.Web` provider
registration. The checked-in icons are scaffold defaults.

All targets need Node.js 22.12+ (or 20.19+), npm, and Rust/Cargo via rustup.
Install host prerequisites from the
[Tauri v2 prerequisites](https://v2.tauri.app/start/prerequisites/), then run
from the repository root:

```bash
cd Portal.Client
npm ci
npm run tauri -- dev                     # desktop development
npm run tauri -- build --debug --no-bundle # desktop debug executable
npm run tauri -- build                   # release executable and bundles
```

| Target | Required toolchain | CI verification |
| --- | --- | --- |
| Linux x86_64 and ARM64 | GCC/build-essential, pkg-config, GTK 3 and WebKitGTK 4.1 development libraries, OpenSSL, Ayatana AppIndicator and librsvg development libraries, and patchelf | Builds a debug executable and AppImage on Ubuntu 24.04 runners; runs window smoke and WebDriver tests under Xvfb. AppImage execution and release bundles are unverified. |
| Windows x86_64 (AMD64) and ARM64 | Rust MSVC toolchain, Visual Studio C++ Build Tools with Windows SDK, Microsoft Edge WebView2 Evergreen Runtime and matching Edge WebDriver | Builds debug executables on Windows Server 2022 x64 and Windows 11 ARM64; checks window visibility and runs shared WebDriver tests. Installers, release packaging, and signing are not tested. |
| Android | JDK 17, Android SDK (platform, build-tools, platform-tools), Android NDK, and Rust Android targets | Builds an x86_64 debug APK and runs smoke/UI checks on an API 35 x86_64 emulator. ARM, x86, physical devices, releases, and signing are not covered. |

### Native authentication decision and validation gate

The intended native flow is a **separate public OIDC client** using the system
browser and authorization code flow with PKCE S256. Do not embed the existing
`portal-web` client secret or collect passwords in the app. Portal's existing
confidential web client, `/signin-oidc` callback, and `portal.session` cookie
are for Portal.Web, not the native client. A system browser, the Tauri WebView,
and other applications must not be assumed to share cookies.

This is a design decision, not a working integration. Portal.Web currently
stores no OIDC tokens and has no native bearer-token API authentication. A
future Portal API must explicitly validate native access tokens (issuer,
signature, expiry, audience and intended token type), obtain trusted group
claims, and apply the same role-based authorization as the web UI and content
endpoints. An ID token is not an API access token. The native client must use
only a configured, trusted HTTPS issuer and Portal API endpoint, not endpoints
or token destinations supplied by a callback. Until that API and a compatible
Authelia public-client registration are proven, do not ship authenticated
native requests or silently fall back to password, WebView, or cookie sharing.
If Authelia cannot support the chosen flow and API audience/claims, revisit the
decision rather than treating the existing web cookie as an API session.

Use a fresh PKCE verifier and unpredictable state for each authorization, plus
OIDC nonce validation; keep the verifier and pending transaction in memory,
bind the callback to that transaction, and consume it only once. Prefer a
claimed HTTPS app link on Android and a loopback redirect bound only to
loopback on Windows and Linux; verify that the actual Authelia registration
supports the selected redirect and port behavior. A custom scheme is not
exclusive to the app and must not be treated as proof of the sender. Reject
unexpected redirect URI, state, issuer, nonce, duplicate, expired, unsolicited,
or malformed callbacks; never send a code or verifier to an untrusted endpoint.
Handle cancellation, browser errors, and app restart without accepting a stale
transaction.

Keep access tokens in memory where practical. Persist refresh tokens only if
needed, using Android Keystore-backed encrypted storage, Windows protected
credential storage, or Linux Secret Service backed by an unlocked keyring;
fail closed or require sign-in again if secure storage is unavailable. Never
store credentials or tokens in plaintext, WebView localStorage, source files,
logs, or crash reports. A compromised process or unlocked device may still
access an active session; PKCE mitigates intercepted authorization codes, not
malicious code running as the user. Protect transport with HTTPS and redact
codes, tokens, cookies, state, and verifiers from diagnostic evidence.

Native logout must clear the app's in-memory and securely stored credentials
and revoke refresh tokens when the provider supports revocation. It must not
claim to sign out the browser, Authelia, or other apps unless that separate
provider logout is actually performed and verified. Portal.Web's existing
logout clears only its own cookie, leaving the identity-provider session active.

#### Required validation before implementation is accepted

No native authentication or Authelia interoperability is validated by the
current smoke tests. The local Aspire topology uses Keycloak, not Authelia.
The following matrix is **not yet run**; record the app revision, OS/device and
browser versions, Authelia version and non-secret client configuration,
redirect URI, observed result, and sanitized evidence for each row. Use a
disposable account and a real Authelia instance with a separately registered
public client; never record credentials, tokens, or authorization codes.

| Target | Required callback and session checks | Status |
| --- | --- | --- |
| Linux x86_64 and ARM64 | System browser and loopback callback on each architecture; verify port registration and behavior with another app listening or claiming a callback. | Not run |
| Windows x86_64 and ARM64 | System browser and loopback callback on each architecture; verify port registration, app activation, and callback handling with another app listening or claiming a callback. | Not run |
| Android | System browser and verified HTTPS app link on an API 35 x86_64 emulator and a physical ARM device; test cold and running app, link verification, and an unverified or competing handler. | Not run |

For **each** row, exercise a successful fresh sign-in and browser SSO sign-in,
and confirm the browser, not the WebView, displays Authelia. Verify PKCE S256
and exact redirect matching with the real provider, reject wrong or missing
state/nonce, replayed and unsolicited callbacks, and verify cancellation,
denial, timeout, and restart during sign-in. Confirm token signature, issuer,
expiry, audience and groups against the Portal API, including denied access
for an unauthorized group and rejection of an ID token. Test expiry, refresh
if used, secure-storage unavailability, and logout followed by a protected API
request and a new sign-in; document whether the browser's Authelia session
remains active and whether provider revocation/logout actually succeeds.
Also verify that neither the Tauri WebView nor another app gains a Portal
session merely because the system browser signed in. Existing Keycloak and
mock OIDC tests are useful regression checks but do not substitute for these
Authelia and platform checks.

### Linux client checks

On Ubuntu/Debian, install the native packages with:

```bash
sudo apt-get update
sudo apt-get install build-essential pkg-config libwebkit2gtk-4.1-dev \
  libssl-dev libayatana-appindicator3-dev librsvg2-dev patchelf \
  xvfb xauth xdotool x11-utils webkit2gtk-driver
```

To reproduce CI on a native x86_64 or ARM64 host:

```bash
cd Portal.Client
npm ci
cargo install tauri-driver --version 2.1.0 --locked
npm run tauri -- build --debug --bundles appimage
xvfb-run -a npm run test:e2e
cd ..
xvfb-run -a bash Portal.Client/smoke-linux.sh
```

The smoke check waits up to 30 seconds for a visible `Portal` window belonging
to the debug executable, then verifies that both remain present for five
seconds. It cleans up the process and prints client output and the X11 window
tree on failure. The AppImage is created in
`Portal.Client/src-tauri/target/debug/bundle/appimage/` and uploaded by CI, but
is not run by the smoke check.

### Windows client checks

To reproduce on an interactive Windows desktop, install Node.js 24, Visual
Studio C++ Build Tools with Windows SDK, rustup, WebView2 Evergreen Runtime, and
the matching Edge WebDriver as `msedgedriver.exe` on `PATH`. Run the tests in a
non-elevated PowerShell 7 session: WebView2 150+ ignores WebDriver's debugging
arguments in elevated applications. CI runs the shared tests at medium
integrity using gsudo.

Run from the repository root (replace `x86_64-pc-windows-msvc` with
`aarch64-pc-windows-msvc` on ARM64):

```powershell
rustup toolchain install stable-x86_64-pc-windows-msvc --profile minimal
rustup default stable-x86_64-pc-windows-msvc
cargo install tauri-driver --version 2.1.0 --locked
npm ci --prefix Portal.Client
Push-Location Portal.Client
npm run tauri -- build --debug --no-bundle
$env:PORTAL_CLIENT_TEST_ADAPTER = './adapters/windows-tauri.js'
npm run test:e2e
Pop-Location
./Portal.Client/smoke-windows.ps1
```

The `.exe` is `Portal.Client/src-tauri/target/debug/portalclient.exe`. The
smoke test checks the registered WebView2 runtime and waits up to 60 seconds
for the visible `Portal` window, then verifies it and the process for 10
seconds. CI uploads the debug executable, screenshot, window, and driver logs.
These checks do not cover portal integration, authentication, installers,
release packaging, or signing.

### Android client checks

The Android workflow uses Ubuntu 24.04, JDK 17, SDK platform 37.0,
build-tools 37.0.0, NDK 27.3.13750724, and the `x86_64-linux-android` Rust
target. It builds a debug APK, installs it on an API 35 Google APIs x86_64
emulator, launches `app.swick.portal.client/.MainActivity`, and checks that the
same process stays in the foreground for 10 seconds without a recorded crash.
CI uploads a screenshot and activity/logcat diagnostics from
`Portal.Client/artifacts/`; failures also print diagnostics in the job log.

The shared tests then run against the installed app using Appium's UiAutomator2
driver for native visibility and screenshots. Since Android accessibility does
not reliably expose the HTML heading role, the heading assertion uses Chrome
DevTools Protocol against the debug APK's actual WebView DOM. It checks a
visible `main h1` through an adb forward to the app process, not a separate
browser. Appium and UiAutomator2 must be installed locally for reproduction;
their pinned versions require Node.js 22.22.2+ or 24.15.0+ (or 26+). The
uploaded debug APK is for inspection only; it is not a portal connectivity,
release artifact, or signing test.

To reproduce on Linux, install Node.js and Rust as above, JDK 17, and the
Android SDK command-line tools. Set `JAVA_HOME` and `ANDROID_HOME`, add
`$ANDROID_HOME/cmdline-tools/latest/bin` and `$ANDROID_HOME/platform-tools` to
`PATH`, and accept the SDK licenses. Install the same packages and build the
APK:

```bash
sdkmanager "platform-tools" "platforms;android-37.0" "build-tools;37.0.0" \
  "ndk;27.3.13750724" "emulator" "system-images;android-35;google_apis;x86_64"
export NDK_HOME="$ANDROID_HOME/ndk/27.3.13750724"
rustup target add x86_64-linux-android
cd Portal.Client
npm ci
npm install --global appium@4.0.1
appium driver install uiautomator2@10.4.0
npm run tauri -- android init --ci --skip-targets-install
npm run tauri -- android build --ci --debug --apk --target x86_64
cd ..
echo no | avdmanager create avd -n portal-smoke \
  -k "system-images;android-35;google_apis;x86_64"
emulator -avd portal-smoke -no-window -gpu swiftshader_indirect -noaudio -no-boot-anim &
adb wait-for-device
until [ "$(adb shell getprop sys.boot_completed | tr -d '\r')" = 1 ]; do sleep 2; done
bash Portal.Client/scripts/android-smoke.sh install
bash Portal.Client/scripts/android-smoke.sh smoke
bash Portal.Client/scripts/android-smoke.sh ui
adb emu kill
```

Use a hardware-accelerated emulator (KVM on Linux) and add
`$ANDROID_HOME/emulator` to `PATH`. The smoke script clears logcat on the
selected device; use a dedicated emulator and ensure only one device is
connected. Android initialization generates `src-tauri/gen/android` locally;
it and `dist`, `node_modules`, Cargo's `target`, and generated bundles are not
committed. Release Android packages require signing configuration.

### Client test architecture

The shared tests in `Portal.Client/tests/client.test.js` define the UI
assertions once. They use a small adapter contract (`launch`, `isVisible`,
`captureScreenshot`, `diagnostics`, and `cleanup`) selected with
`PORTAL_CLIENT_TEST_ADAPTER`. Each adapter exports `createAdapter()` returning
those async methods; `isVisible({ role, text })` checks exact visible text,
`captureScreenshot(file)` writes to the supplied path, and `diagnostics()`
returns a log string.

The Linux and Windows Tauri/WebDriver adapters drive the actual native webview.
The Android adapters run against the installed app. Selenium tests check the
placeholder heading and text; they do not test portal connectivity,
authentication, release bundles, or installers. CI uploads screenshots and
diagnostic logs from `Portal.Client/artifacts/`, including when tests fail.

## CI and contribution checks

The workflows under `.github/workflows` run on pull requests to `main` and on
pushes to `main` or `v*` tags. The main build workflow restores, builds, and
tests `Portal.slnx`, validates container packaging without pushing, lints the
Helm chart, and builds/tests the Linux and Windows native clients on x86_64 and
ARM64. The separate Android workflow builds and smoke-tests an x86_64 debug
APK. Successful pushes publish the portal image to
`ghcr.io/twsouthwick/portal` and the Helm chart to
`oci://ghcr.io/twsouthwick/charts/portal`.

The image uses the MinVer version and `latest` tags; the chart uses the same
MinVer version. Both branch and tag pushes update `latest`. Publishing uses
the workflow's `GITHUB_TOKEN` with package-write permission. Existing GHCR
packages must grant this repository's Actions workflow access. New packages
are private by default; pulling them requires registry credentials unless
their visibility is changed.

The container uses the .NET 11 Ubuntu Resolute chiseled-extra ASP.NET image
from MCR. The build job validates container packaging into a temporary archive,
including the version and `latest` tags, without pushing. This requires access
to MCR and its blob storage. Actual GHCR image and Helm chart pushes run only
on successful branch or tag pushes, so verify both publishing steps after
merging; pull requests cannot validate GHCR package permissions or pushes.

The independent `Native client release builds` workflow also runs on these
events and on manual dispatch. It is configured to build release AppImages on
native Linux x86_64 and ARM64 runners, `portalclient.exe` executables on native
Windows x86_64 and ARM64 runners, and an x86_64 Android APK.
Each job checks the output architecture and launches
the app before uploading a workflow artifact named with the checked-out commit
SHA. The Android job tests a temporary, locally signed copy on an API 35
x86_64 emulator; the uploaded release APK is unsigned and must be signed
separately before installation or distribution. Windows binaries are also
unsigned. No distribution signing keys are used by this workflow.

The workflow pins Node and Rust versions, uses `npm ci` and Cargo's locked
dependencies, and sets `SOURCE_DATE_EPOCH` from the commit. Artifacts include
SHA-256 checksums and commit/toolchain provenance. Hosted runner images,
system packages, Android/Gradle dependencies and packaging tools can change, so
bit-for-bit identical rebuilds are not guaranteed. Workflow artifacts are
retained according to repository settings, not published as permanent GitHub
releases. The new release targets should not be considered verified until
their respective jobs succeed on actual runners. Android ARM devices, Windows
installers, code signing, and portal functionality are not covered.

For changes, run the focused checks for the affected project and the solution
build/tests. Native client changes should also be checked with the relevant
platform smoke and end-to-end tests where that platform is available. Generated
output such as `node_modules`, `Portal.Web/wwwroot/dist`, client build output,
and Android generated files should not be committed.
