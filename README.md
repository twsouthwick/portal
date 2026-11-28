# Portal - Intranet Portal System

A Blazor-based intranet portal built with .NET 11 and Aspire. Portal brings
multiple subsites together with OpenID Connect authentication and role-based
access control.

## Features

- **Role-based access control** for subsites and their static content
- **OpenID Connect authentication** with an identity provider
- **Static content** served alongside each subsite
- **Markdown usage guides** for subsites
- **App protocol launching** with app-store fallbacks
- **Private mode detection** to show private sites only in incognito mode

## Getting started

1. Create a sites directory or ZIP file with a root `manifest.json`.
2. Add a directory and `manifest.json` for each subsite.
3. Optionally add a `usage.md` guide and a `content/` directory to each subsite.
4. Configure `SITE_PATH` to the directory or ZIP file.
5. Configure OIDC as described in [Authentication](#authentication).
6. Deploy Portal with Docker or the [Helm chart](chart/README.md).

See [Subsite configuration](#subsite-configuration) for manifest details. For
local development and contribution instructions, see the
[developer guide](developer.md).

## Native development builds

Successful `main` builds update the rolling `dev` prerelease on the
[GitHub Releases page](https://github.com/twsouthwick/portal/releases).
Stable `vMAJOR.MINOR.PATCH` tags create immutable releases. New release titles
are the exact MinVer semantic version. Both channels include Linux and Windows
AppImages/executables for x86_64 and ARM64 plus a signed Android APK containing
`arm64-v8a` and `x86_64`; checksums and build
provenance accompany the assets. The Android APK is smoke-tested on an x86_64
emulator, while the ARM64 libraries are inspected but not device-tested.

The desktop binaries are unsigned; Windows needs the WebView2 runtime, and
Linux needs a compatible desktop and AppImage/FUSE support (or extraction).
The client is a placeholder and does not connect to Portal. See the
[developer guide](developer.md#ci-and-contribution-checks) for release channels,
verification, provenance, and signing details.

## Authentication

Portal uses the OpenID Connect authorization code flow with PKCE. Anonymous
requests are challenged immediately, and the resulting identity is stored in
an HTTP-only application cookie.

Configure a confidential OIDC client with this redirect URI:

```text
https://<portal-host>/signin-oidc
```

The ID token must contain a `groups` claim whose values match the roles in site
manifests. The `name` claim is used as the display name.

Configure the application with:

- `Oidc__Authority`: OIDC authority, including the realm or tenant path
- `Oidc__ClientId`: confidential client identifier
- `Oidc__ClientSecret`: confidential client secret
- `Oidc__RequireHttpsMetadata`: defaults to `true`; disable only for local development

Signing out clears only the Portal application cookie and leaves the
identity-provider session active. The signed-out page provides a link to start
a new local session.

## Configuration

### Site sources

When `SITE_PATH` is set, Portal reads sites from that directory or ZIP file.
When running in Kubernetes or when a kubeconfig is present, it can also read
`PortalSite` resources from all namespaces. `PORTAL_KUBERNETES_NAMESPACE` is no
longer used.
If both sources contain the same site ID, the file entry wins. Portal name and
description come from the file source when present. If neither source is
configured, the bundled `sites` directory is used.

Kubernetes uses the in-cluster service account or the default kubeconfig. The
service account needs permission to list PortalSites and get ConfigMaps in
all namespaces. Referenced ConfigMaps are read in the PortalSite's own
namespace, and site IDs must be unique across namespaces. Once configured,
Kubernetes errors are surfaced rather than silently ignored. File manifests are
loaded at startup.

The [Helm chart](chart/README.md) installs the CRD and configures these
permissions. Changes to Kubernetes resources are picked up on subsequent
portal loads.

### Portal manifest

Create a `manifest.json` in the root of your sites directory:

```json
{
  "name": "Family Home Portal",
  "description": "Central hub for family resources and home services"
}
```

### Subsite configuration

Each subsite requires its own directory with a `manifest.json` file:

```text
sites/
├── manifest.json
├── home-assistant/
│   ├── manifest.json
│   ├── usage.md (optional)
│   └── content/ (optional)
└── jellyfin/
    ├── manifest.json
    ├── usage.md (optional)
    └── content/ (optional)
```

Example subsite manifest:

```json
{
  "id": "audiobooks",
  "name": "Audiobookshelf",
  "description": "Audiobook and podcast library management",
  "icon": "bi bi-headphones",
  "url": "https://audiobooks.example.com",
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
    }
  ],
  "categories": ["entertainment"],
  "roles": ["audiobookshelf"]
}
```

Manifest properties:

- `id` (required): Unique identifier matching the directory name
- `name` (required): Display name
- `description`: Brief description
- `url`: Primary HTTP or HTTPS URL for the subsite
- `apps`: Apps that can be launched for the subsite; each has a display `name`,
  a protocol `scheme`, and optional `store` links
- `icon`: Bootstrap icon class (default: `bi bi-globe`)
- `categories`: Categories the subsite belongs to
- `roles`: Required roles for access; an empty or missing list makes the site public

### Usage guides and static content

Add a `usage.md` file to a subsite directory for Markdown instructions. Place
files in its `content/` subdirectory; they are served at
`/{subsite-id}/content/{path}`. For example:

```text
home-assistant/
├── manifest.json
├── usage.md
└── content/
    ├── logo.png
    └── guides/
        └── setup.pdf
```

The PDF above is available at
`https://portal.example.com/home-assistant/content/guides/setup.pdf`.

### Role-based access

Add a `roles` array to a subsite manifest to restrict access:

```json
{
  "id": "router-admin",
  "name": "Router Admin",
  "url": "https://router.example.com",
  "roles": ["admin"]
}
```

Users need at least one matching role to see the subsite or access its static
content. Empty or missing `roles` makes both public.

### Managing subsites

Add a subsite directory and its manifest under the configured sites directory,
then restart Portal to load file-based changes. No application code changes are
needed.

### Icons

The `icon` property accepts Bootstrap Icons classes. Common examples are
`bi bi-house-door`, `bi bi-camera-video`, `bi bi-film`, `bi bi-router`,
`bi bi-cloud-sun`, `bi bi-shield-check`, and `bi bi-server`. See the full
[Bootstrap Icons](https://icons.getbootstrap.com/) set.

## Deployment

Example `docker-compose.yml`:

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

Authenticate Docker to GHCR before starting a private image. Configure the OIDC
settings above for the deployed portal. For Kubernetes installation and
configuration, see the [Helm chart documentation](chart/README.md).

Main builds publish the mutable `dev` image tag; stable `vMAJOR.MINOR.PATCH`
releases publish version, major/minor, major, and `latest` tags. The Helm chart
uses its matching `appVersion` by default.

## Further documentation

- [Developer guide](developer.md): local setup, architecture, build, tests, and CI
- [Helm chart](chart/README.md): Kubernetes installation and configuration
