# Portal - Intranet Portal System

A Blazor-based intranet portal system built with .NET 11 and Aspire that allows you to manage and display multiple subsites with role-based access control.

## Features

- **Role-Based Access Control**: Restrict subsites to specific user roles
- **OpenID Connect Authentication**: Integrate directly with an OIDC identity provider
- **Static Content Serving**: Each subsite can serve its own static files
- **Markdown Documentation**: Include usage instructions in Markdown format
- **App Protocol Launching**: Launch native apps with fallback to app stores
- **Private Mode Detection**: Automatically hide private sites when not in incognito mode

## Quick Start

1. Create a sites directory (or ZIP file) with a `manifest.json` file
2. Add subdirectories for each subsite with their own `manifest.json` files
3. Optionally add `usage.md` and `content/` folders for documentation and static files
4. Set `SITE_PATH` environment variable to your sites directory or ZIP file path
5. Deploy using docker

## Development

### Prerequisites

- .NET 11 SDK
- Node.js (for Webpack/TypeScript compilation)

### Devcontainer (Aspire)

This repository includes a VS Code devcontainer for local Aspire development. Reopen the folder in the devcontainer and then run:

```bash
aspire run
```

The container installs the pinned .NET 11 SDK from `global.json`, the matching Aspire CLI, Node.js 24, and Docker-in-Docker support so the AppHost can launch the `media` and `admin` portal instances.

### Building

The project uses **Webpack** to bundle TypeScript, CSS, and static assets. All source files are in `Portal.Web/TypeScript/` and are compiled to `wwwroot/dist/`.

**Build Commands:**

```bash
cd Portal.Web

# Install dependencies (first time only)
npm install

# Production build (minified)
npm run build

# Development build (with source maps)
npm run dev

# Watch mode for development
npm run watch

# Clean generated files
npm run clean
```

### GitHub Actions

Pull requests to `main` and pushes to `main` or `v*` tags build and test the solution and lint the Helm chart. Successful pushes also publish the portal image to `ghcr.io/twsouthwick/portal` and the Helm chart to `oci://ghcr.io/twsouthwick/charts/portal`. The image receives the MinVer version and `latest` tags; the chart uses the same MinVer version. Both branch and tag pushes update `latest`.

Publishing uses the workflow's `GITHUB_TOKEN` with package-write permission. Existing GHCR packages must grant this repository's Actions workflow access. New packages are private by default; pulling them requires registry credentials unless their visibility is changed.

### Native client scaffold

`Portal.Client` is a standalone Tauri v2 shell. It displays a placeholder and
does not yet connect to the portal, implement authentication, or change
`Portal.Web` provider registration. The checked-in icons are scaffold defaults.

All targets need Node.js 22.12+ (or 20.19+), npm, and Rust/Cargo via rustup.
Install the prerequisites for your host from the
[Tauri v2 prerequisites](https://v2.tauri.app/start/prerequisites/), then run
from the repository root:

```bash
cd Portal.Client
npm ci
npm run tauri -- dev                     # desktop development
npm run tauri -- build --debug --no-bundle # desktop debug executable
npm run tauri -- build                   # release executable and bundles
```

| Target | Required toolchain | Verification in this scaffold |
| --- | --- | --- |
| Linux x86_64 | GCC/build-essential, pkg-config, GTK 3 and WebKitGTK 4.1 development libraries, OpenSSL, Ayatana AppIndicator and librsvg development libraries, and patchelf | Ubuntu 24.04 x86_64: debug build without bundling succeeded; window opened under Xvfb. Release bundles and other architectures unverified. |
| Windows x86_64 | Rust MSVC toolchain, Visual Studio C++ Build Tools with Windows SDK, and Microsoft Edge WebView2 Evergreen Runtime | CI builds an unbundled debug shell on Windows Server 2022 and checks that its `Portal` window opens and remains visible for 10 seconds. Other architectures, installers, release packaging, and signing are not tested. |
| Android | A supported desktop host's prerequisites, JDK 17, Android Studio/SDK (platform, build-tools, platform-tools), Android NDK, and Rust Android targets | Unverified; no Android build, emulator, or device tested. |

For Ubuntu/Debian Linux the native packages can be installed with:

```bash
sudo apt-get install build-essential pkg-config libwebkit2gtk-4.1-dev \
  libssl-dev libayatana-appindicator3-dev librsvg2-dev patchelf
```

To reproduce the Windows x86_64 CI check in an interactive Windows desktop,
install Node.js 24, the Visual Studio C++ Build Tools with Windows SDK, rustup,
and the WebView2 Evergreen Runtime. From the repository root in PowerShell 7:

```powershell
rustup toolchain install stable-x86_64-pc-windows-msvc --profile minimal
rustup default stable-x86_64-pc-windows-msvc
npm ci --prefix Portal.Client
Push-Location Portal.Client
npm run tauri -- build --debug --no-bundle
Pop-Location
./Portal.Client/smoke-windows.ps1
```

The smoke test checks the registered WebView2 Evergreen Runtime, launches the
debug executable, waits up to 60 seconds for its visible `Portal` window, and
checks the window and process for 10 seconds. It prints captured output and
cleans up the process tree. This verifies window startup only, not page content,
portal integration, authentication, installers, release packaging, or signing.
The client `dist`, `node_modules`, and Cargo `target` directories are generated
and gitignored.

For Android, configure `JAVA_HOME`, `ANDROID_HOME`, and `NDK_HOME` for the
installed JDK, SDK, and NDK, then install the Rust targets for the ABIs you
intend to build. For example, for an ARM64 APK:

```bash
rustup target add aarch64-linux-android
cd Portal.Client
npm ci
npm run tauri -- android init
npm run tauri -- android dev             # requires a device or emulator
npm run tauri -- android build --debug --apk --target aarch64
```

Android initialization generates `src-tauri/gen/android` locally; it and
`dist`, `node_modules`, Cargo's `target`, and generated bundles are not
committed. Release Android packages require signing configuration.

### Project Structure

```
Portal.Web/
├── TypeScript/
│   ├── app.ts                      # Main entry point - exports all helper functions
│   ├── app.css                     # Main stylesheet (imports bootstrap-icons)
│   └── components/
│       ├── siteLauncher.ts         # URL launching helpers (openUrlInNewTab, launchAppUrl)
│       └── siteProvider.ts         # Browser detection (isPrivateMode)
├── Components/
│   ├── App.razor                   # References ./dist/app.css
│   ├── SiteLauncher.razor          # Imports ./dist/app.js (uses openUrlInNewTab, launchAppUrl)
│   └── SiteProvider.razor          # Imports ./dist/app.js (uses isPrivateMode)
├── wwwroot/
│   └── dist/                       # Webpack output (generated, gitignored)
│       ├── app.js                  # Bundled JavaScript module with exported functions
│       ├── app.css                 # Bundled CSS (includes bootstrap-icons)
│       └── fonts/                  # Bootstrap icon fonts
├── node_modules/                   # npm packages (gitignored)
├── webpack.config.js               # Webpack configuration
├── tsconfig.json                   # TypeScript configuration
└── package.json                    # npm dependencies & scripts
```

### TypeScript Helper Functions

The compiled `app.js` module exports three helper functions for use in Blazor components:

- **`isPrivateMode(): Promise<boolean>`** - Detects if the browser is in private/incognito mode
- **`launchAppUrl(url: string): Promise<boolean>`** - Attempts to launch an app protocol URL and returns whether it succeeded
- **`openUrlInNewTab(url: string): void`** - Opens a URL in a new browser tab

**Usage in Blazor:**

```csharp
// Import the module
var module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./dist/app.js");

// Call helper functions
var isPrivate = await module.InvokeAsync<bool>("isPrivateMode");
var launched = await module.InvokeAsync<bool>("launchAppUrl", "myapp://launch");
await module.InvokeVoidAsync("openUrlInNewTab", "https://example.com");
```

### What Webpack Does

- ✅ Compiles TypeScript to JavaScript
- ✅ Bundles all JavaScript modules into `app.js` as an ES module
- ✅ Processes and bundles CSS (including bootstrap-icons)
- ✅ Copies font files to output directory
- ✅ Generates source maps for debugging (dev mode)
- ✅ Minifies output (production mode)

**All dependencies are managed via npm** - LibMan has been removed.

## Authentication

The portal uses OpenID Connect authorization code flow with PKCE. Anonymous requests to the application are challenged immediately. The resulting identity is stored in an HTTP-only application cookie.

Configure a confidential OIDC client with this redirect URI:

```text
https://<portal-host>/signin-oidc
```

The ID token must contain a `groups` claim whose values match the roles in site manifests. The `name` claim is used as the display name.

Configure the application with:

- `Oidc__Authority`: OIDC authority, including the realm or tenant path
- `Oidc__ClientId`: confidential client identifier
- `Oidc__ClientSecret`: confidential client secret
- `Oidc__RequireHttpsMetadata`: defaults to `true`; disable only for local development

Signing out clears only the Portal application cookie and leaves the identity-provider session active. The signed-out page provides a link to start a new local session.

### Local Development

`aspire run` starts Keycloak at `http://localhost:8080` and imports the development realm. Use either development account:

| Username | Password | Groups |
| --- | --- | --- |
| `admin` | `admin` | `admin`, `media` |
| `media` | `media` | `media` |

The local credentials and client secret are development-only.

## Configuration

### Site provider

The portal automatically composes site sources. When `SITE_PATH` is set, it
reads that directory or ZIP file. When running in Kubernetes or when a
kubeconfig is present, it also reads `PortalSite` resources from
`PORTAL_KUBERNETES_NAMESPACE` (default `default`). If both sources contain the
same site ID, the file entry wins. Portal name and description come from the
file source when present. If neither source is configured, the bundled `sites`
directory is used.

Kubernetes uses the in-cluster service account or the default kubeconfig. The
service account needs permission to list PortalSites and get ConfigMaps in that
namespace. Once configured, Kubernetes errors are surfaced rather than silently
ignored. File manifests are loaded at startup.

Providers are registered in `Program.cs` with
`AddPortalProvider(portal => portal.AddFile().AddKubernetes())`; registration
order determines precedence.

The [Helm chart](chart/README.md) installs the CRD and configures these
permissions. Changes to resources are picked up on subsequent portal loads.

### Portal Configuration

Create a `manifest.json` in your sites directory root:

```json
{
  "name": "Family Home Portal",
  "description": "Central hub for family resources and home services"
}
```

### Subsite Configuration

Each subsite requires its own directory with a `manifest.json` file:

```
sites/
├── manifest.json
├── home-assistant/
│   ├── manifest.json
│   ├── usage.md (optional)
│   └── content/ (optional)
└── jellyfin/
    ├── manifest.json
    ├── usage.md
    └── content/
```

**Manifest Properties:**

```json
{
  "id": "audiobooks",
  "name": "Audiobookshelf",
  "description": "Audiobook and podcast library management",
  "icon": "bi bi-headphones",
  "url": "https://audiobooks.swick.app",
  "apps": [
    {
      "name": "Audiobookshelf",
      "scheme": "audiobookshelf://",
      "store": [
        {
          "name": "google-play",
          "id": "https://play.google.com/store/apps/details?id=com.audiobookshelf.app"
        }
      ]
    },
    {
      "name": "plappa",
      "scheme": "plappa://",
      "store": [
        {
          "name": "apple",
          "id": "https://apps.apple.com/us/app/plappa/id6475201956"
        }
      ]
    }
  ],
  "categories": ["entertainment"],
  "roles": ["audiobookshelf"]
}
```

- `id` (required): Unique identifier matching directory name
- `name` (required): Display name
- `description`: Brief description
- `url`: Primary URL for the subsite (HTTP/HTTPS)
- `apps`: Array of apps that can be launched for this subsite
  - `name`: Display name of the app
  - `scheme`: App protocol/scheme (e.g., "audiobookshelf://", "plappa://")
  - `store`: Array of store links where the app can be downloaded
    - `name`: Store identifier (e.g., "google-play", "apple", "f-droid")
    - `id`: Store URL or app identifier
- `icon`: Bootstrap icon class for the card (default: "bi bi-globe")
- `categories`: Array of categories the subsite belongs to
- `roles`: Required roles for access (empty = public)

### Usage Documentation

Add a `usage.md` file with Markdown instructions:

```markdown
# Home Assistant Usage Guide

1. Click the link to open Home Assistant
2. Log in with your credentials
3. Navigate to the dashboard

![Dashboard](content/screenshots/dashboard.png)
```

### Static Content

Place files in the `content/` subdirectory. Access at `/{subsite-id}/content/{path}`:

```
home-assistant/
├── manifest.json
└── content/
    ├── logo.png
    └── guides/
        └── setup.pdf
```

Access: `https://portal.home.arpa/home-assistant/content/guides/setup.pdf`

## Role-Based Access Control

Control access by adding a `roles` array:

```json
{
  "id": "router-admin",
  "name": "Router Admin",
  "url": "https://router.home.arpa",
  "roles": ["admin"]
}
```

- Empty/missing `roles` = visible to all users
- With `roles` = user must have at least one matching role
- Applies to both subsite cards and static content

## Deployment

**docker-compose.yml:**
```yaml
services:
  portal:
    image: ghcr.io/twsouthwick/portal:latest
    ports:
      - "8080:8080"
    environment:
      - SITE_PATH=/sites
    volumes:
      - ./my-sites:/sites:ro
    restart: unless-stopped
```

Authenticate Docker to GHCR before starting a private image. Configure the OIDC settings described above for the deployed portal.

## Dynamic Site Management

Add or update subsites without code changes:

1. Create a directory under your sites folder
2. Add `manifest.json` with required properties
3. Optionally add `usage.md` and `content/` folder
4. Restart the portal container

## Common Icons

- `bi bi-house-door` - Home Assistant, Dashboard
- `bi bi-camera-video` - Security Cameras, Frigate
- `bi bi-film` - Jellyfin, Media Servers
- `bi bi-router` - Network Equipment
- `bi bi-cloud-sun` - Weather Stations
- `bi bi-shield-check` - Pi-hole, Security
- `bi bi-server` - NAS, Home Servers

See the full icon set at: https://icons.getbootstrap.com/
