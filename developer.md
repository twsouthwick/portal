# Developer Guide

For Portal features, configuration, and deployment, see the [user guide](README.md).
This document covers local development, implementation boundaries, builds, tests,
the native client, and CI.

## Local setup

The repository pins the .NET SDK in [`global.json`](global.json) (currently
.NET 11.0.100). The web frontend and native client use Node.js; CI uses Node.js
24. Install the Aspire CLI 17.0.0 to run the local application topology.

The VS Code devcontainer installs the pinned SDK, Aspire CLI, Node.js 24, and
Docker-in-Docker. The local identity provider also needs Docker, OpenSSL, and a
trusted .NET development HTTPS certificate. The following setup is for a local,
single-user development machine, not a shared or production deployment.

Run these commands in the same environment as the AppHost and Docker daemon:

```bash
dotnet dev-certs https --trust
dotnet dev-certs https --check --trust
mkdir -p "$HOME/.aspnet/portal-authelia"
chmod 700 "$HOME/.aspnet/portal-authelia"
dotnet dev-certs https --format PEM --no-password -ep "$HOME/.aspnet/portal-authelia/cert.pem"
openssl genrsa -out "$HOME/.aspnet/portal-authelia/oidc.pem" 2048
chmod 600 "$HOME/.aspnet/portal-authelia/cert.key" "$HOME/.aspnet/portal-authelia/oidc.pem"
dotnet user-secrets set Parameters:portal-client-secret portal-development-secret --project Portal.AppHost
for name in authelia-session-secret authelia-storage-key authelia-oidc-hmac-secret; do
  dotnet user-secrets set "Parameters:$name" "$(openssl rand -base64 48)" --project Portal.AppHost
done
```

The export contains an **unencrypted private key**. Keep the directory private,
never commit or share it, and do not expose this development identity provider
to an untrusted network. The sample accounts, one-factor policy, and client
secret are deliberately insecure and local-only. User secrets are stored
unencrypted on the development machine. The client secret must match the
development-only hash in `Portal.AppHost/Authelia/configuration.yml`.

The .NET 11 development certificate includes `localhost` and
`*.dev.localhost`; an older certificate may not. Check with
`openssl x509 -in "$HOME/.aspnet/portal-authelia/cert.pem" -noout -ext subjectAltName`.
If necessary, run `dotnet dev-certs https --clean`, trust a newly generated
certificate, and repeat the export. On Linux, .NET/OpenSSL may require
`SSL_CERT_DIR="$HOME/.aspnet/dev-certs/trust:/usr/lib/ssl/certs"` in the
AppHost environment. Follow the [dev-certs documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-dev-certs)
for platform/browser trust; `--check --trust` must succeed and the browser
must trust the same certificate. Ensure `auth.dev.localhost` resolves to the
development host (normally loopback) and is excluded from any HTTP proxy.

In a devcontainer, the Docker daemon must be able to access the exported files
at the same absolute path. Port-forward 8080, 6100 and 6110 to the browser host,
and trust the *public* development certificate in that browser's trust store;
trust inside the devcontainer alone does not establish host browser trust. Do
not copy the private key to the browser host. The Portal profiles run as
processes alongside the AppHost, not as containers: both their backchannel and
the browser use `https://auth.dev.localhost:8080` as the issuer. A separately
containerized Portal requires explicit DNS/routing to the host and the public
certificate in its trust store; `localhost` inside it is not the host. This
topology does not configure that separate container deployment.

Then run:

```bash
aspire run
```

The AppHost starts Authelia at `https://auth.dev.localhost:8080` and launches
the `media` (`https://localhost:6110`) and `admin`
(`https://localhost:6100`) web profiles. Use either development account:

| Username | Password | Groups |
| --- | --- | --- |
| `admin` | `admin` | `admin`, `media` |
| `media` | `media` | `media` |

Authelia stores its local database in the Docker volume `portal-authelia-data`.
To reset its sessions and consent, stop Aspire and remove that volume with
`docker volume rm portal-authelia-data`. Changing the storage encryption key
also requires resetting the volume. Re-export the HTTPS certificate after
regenerating it, and restart Aspire; regenerate `oidc.pem` only when prepared to
invalidate existing OIDC signing keys. To remove local secrets, use
`dotnet user-secrets clear --project Portal.AppHost` and remove the private
directory. Portal logout clears its own cookie, not the Authelia session.

The `portal-web` client is confidential and uses authorization code with PKCE
and the `groups` scope. It is not a native public client; native authentication
and callback compatibility are tracked separately in issue #11.

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
