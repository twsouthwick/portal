# Portal - Intranet Portal System

A Blazor-based intranet portal system built with .NET 10 and Aspire that allows you to manage and display multiple subsites with role-based access control.

## Features

- **Role-Based Access Control**: Restrict subsites to specific user roles
- **Trusted Header Authentication**: Integrate with reverse proxies for SSO
- **Static Content Serving**: Each subsite can serve its own static files
- **Markdown Documentation**: Include usage instructions in Markdown format

## Quick Start

1. Create a sites directory (or ZIP file) with a `manifest.json` file
2. Add subdirectories for each subsite with their own `manifest.json` files
3. Optionally add `usage.md` and `content/` folders for documentation and static files
4. Set `SITE_PATH` environment variable to your sites directory or ZIP file path
5. Deploy using docker

## Authentication

The portal uses **Trusted Header Authentication**, which integrates with reverse proxies that handle authentication and pass user information via HTTP headers.

### How It Works

1. A reverse proxy authenticates users (SSO, LDAP, OAuth, etc.)
2. The proxy forwards authenticated requests with user info in headers
3. The portal reads the headers and creates a user session
4. Access is granted based on the user's roles

### Configuration

The portal reads two HTTP headers:

- `REMOTE_USER`: The authenticated username (required)
- `REMOTE_ROLES`: Comma-separated list of user roles (optional)

## Configuration

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
  "id": "home-assistant",
  "name": "Home Assistant",
  "description": "Smart home automation and control",
  "url": "https://homeassistant.home.arpa",
  "icon": "bi bi-house-door",
  "category": "smart-home",
  "roles": ["admin", "family"]
}
```

- `id` (required): Unique identifier matching directory name
- `name` (required): Display name
- `description`: Brief description
- `url`: External URL to link to
- `icon`: Bootstrap icon class (default: "bi bi-globe")
- `category`: Grouping category
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
    build: .
    ports:
      - "8080:8080"
    environment:
      - SITE_PATH=/sites
    volumes:
      - ./my-sites:/sites:ro
    restart: unless-stopped
```

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
