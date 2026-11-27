---
parent: high-level-design
prefix: CLIENT
---
# Native Client

## Context and Design Philosophy

`Portal.Client` is a Tauri v2 desktop/mobile shell with a Vite-served static
frontend. The checked-in UI contains only a heading and placeholder text. The
Rust layer starts the default Tauri application and defines no commands,
application state, IPC, API client, or authentication flow.

## Shell and Build Configuration

Tauri config defines the window, frontend build/dev commands, content security
policy, bundle icons, and mobile entry point. Desktop and Android build/test
automation lives in the GitHub workflows. The shared E2E assertions use
platform adapters for desktop WebDriver and Android UI/WebView inspection.

## Current Boundary

The developer guide states that this client does not yet connect to the portal,
implement authentication, or change server provider registration. Portal
discovery, sign-in, site launch integration, synchronization, and server
interaction are not implemented. The authentication design below is a
proposal, not approved intent or evidence of working integration.

## Proposed Native Authentication

**[owner-selected 2026-11-27; not yet validated]** A separate public OIDC
client using the system browser and authorization-code flow with PKCE S256 is
selected over a Portal-mediated session. The first Portal API to use it is the
client manifest endpoint (`GET /api/manifest`, issue #7), designed in the
identity-and-access LLD. The endpoint accepts signed JWT access tokens with
the configured `Oidc:ApiAudience` (default `portal-api`) and maps trusted
`groups` claims to roles. A native application cannot protect the confidential
`portal-web` client secret. Portal.Web uses its own `/signin-oidc` callback
and `portal.session` cookie and saves no OIDC tokens. The manifest API has a
separate bearer-token policy; the native client's sign-in and API integration
are not implemented. The system browser, Tauri WebView, and other apps cannot
be assumed to share cookies. The native client would not collect passwords or
store credentials in plaintext.

This choice depends on proving that the actual Authelia deployment supports a
separate public client, the platform redirects, and suitable access-token
audience and group claims. Portal.Web validates the issuer, signature,
expiry, and API audience, and applies the relevant role authorization. An ID
token is not an API access token and has a different audience.
Requests would use a configured trusted HTTPS issuer and API endpoint, not
destinations supplied by a callback. If these prerequisites cannot be met,
reconsider a Portal-mediated session/API flow with the owner rather than
assuming the web cookie is usable. The API and server authorization belong
to the identity-and-access boundary; do not cascade changes there without
owner direction.

### Callback and session proposal

The proposed transaction uses a fresh verifier, unpredictable state, and an
OIDC nonce, with the pending transaction held in memory and consumed once.
The candidate redirects are a verified HTTPS app link on Android and a
loopback-only listener on Windows and Linux. Exact redirect and dynamic-port
support remain to be verified against Authelia. Custom schemes can be claimed
by other apps and cannot establish callback authenticity. Validation needs to
cover unexpected or malformed redirects, state, issuer and nonce, replay,
expiry, cancellation, errors and restart; neither codes nor verifiers may be
sent to an untrusted endpoint.

The proposed storage boundary keeps access tokens in memory where practical
and persists refresh tokens only if needed: Android Keystore-backed encrypted
storage, Windows protected credential storage, or Linux Secret Service with an
unlocked keyring. Unavailable secure storage would require sign-in again or a
closed failure. WebView localStorage, plaintext files, logs, and crash reports
are not credential stores. HTTPS protects transport; diagnostic evidence must
not expose codes, tokens, cookies, state, or verifiers. PKCE mitigates code
interception but does not protect an active session from a compromised process
or unlocked device.

The proposed native logout clears in-memory and secure credentials and revokes
refresh tokens where supported. It does not imply browser, Authelia, or other
app logout unless separately performed and verified. Portal.Web currently
clears only its own cookie and leaves the identity-provider session active.

### Validation evidence needed

No native authentication or Authelia interoperability has been validated by
the current smoke tests. The local Aspire topology uses Keycloak, not Authelia.
The following checks are **not run**. Use a disposable account and a real
Authelia instance with a separate public client, and record the app revision,
OS/device and browser versions, Authelia version, non-secret registration
details, redirect URI, observed result, and sanitized evidence. Do not record
credentials, tokens, or authorization codes.

| Target | Proposed platform checks | Status |
|---|---|---|
| Linux x86_64 and ARM64 | System browser and loopback callback on each architecture, port registration, and competing listener/handler. | Not run |
| Windows x86_64 and ARM64 | System browser and loopback callback on each architecture, port registration, app activation, and competing listener/handler. | Not run |
| Android | System browser and verified HTTPS app link on an API 35 x86_64 emulator and physical ARM device; cold and running app, link verification, and unverified or competing handler. | Not run |

On each target, validate fresh and SSO sign-in, PKCE S256, redirect matching,
state and nonce failures, replay and unsolicited callbacks, cancellation,
denial, timeout, and restart. Once a compatible API exists, validate token
signature, issuer, expiry, audience, type and groups, unauthorized-group
denial, expiry and refresh, storage failure, and logout followed by a
protected request and renewed sign-in. Record whether browser SSO remains
active and whether revocation or provider logout actually succeeds. Verify
that browser sign-in does not automatically give the WebView or another app a
Portal session. Keycloak and mock OIDC tests do not establish Authelia or
platform compatibility.

## Decisions & Alternatives

| Decision | Current approach | Alternatives considered | Rationale |
|---|---|---|---|
| Native framework | Tauri v2 with a Vite frontend and minimal Rust bootstrap. **[inferred]** | Browser-only application or another native shell. | These are the checked-in crate and configuration. |
| Current client scope | Placeholder shell only. **[inferred from developer guide]** | Portal-connected client. | The developer guide documents the current non-integration. |
| Native authentication | Separate public OIDC client with system-browser code flow and PKCE S256. **[owner-selected 2026-11-27; not validated]** | Portal-mediated session/API flow. | Avoids embedding the confidential web secret or assuming shared browser cookies; requires provider and API compatibility. |
| UI verification | Shared Node E2E assertions use platform adapters. **[inferred]** | Separate assertions for each platform. | `Portal.Client/tests/client.test.js` shares assertions and adapters. |

## Open Questions & Future Decisions

### Planned

1. Client presentation of manifest flags (planned): show `IsPrivate` sites
   behind a user toggle, and use home-network detection for `IsInternal`
   sites. The issue #6 DTO has no home-network CIDR field;
   [issue #53](https://github.com/twsouthwick/portal/issues/53) tracks it
   separately from the initial manifest and endpoint. For a missing field, #53
   specifies `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, and `fc00::/7`
   as defaults, excluding loopback and link-local ranges. A CIDR match alone
   does not establish a home network or grant access.

### Deferred

1. What user need should the native client solve beyond displaying a shell?
2. Resolved 2026-11-27: the owner selected the public-client model. Its
   Authelia and platform prerequisites remain unvalidated.
3. Which desktop and mobile targets are release requirements?
4. Release installers, signing, production packaging, and portal integration are
   not verified by the current scaffold checks.

## References

- HLD: `docs/high-level-design.md` — Approach and System Design
- EARS: `docs/intent/native-client/native-client-specs.md`
- Product scope and platform checks: `developer.md` — Native client
- Server identity boundary: `docs/intent/identity-and-access/identity-and-access-design.md`
- UI and Vite: `Portal.Client/index.html`, `Portal.Client/vite.config.ts`,
  `Portal.Client/package.json`
- Tauri host: `Portal.Client/src-tauri/`
- Tests and adapters: `Portal.Client/tests/`
- Smoke/setup scripts: `Portal.Client/smoke-linux.sh`,
  `Portal.Client/smoke-windows.ps1`,
  `Portal.Client/scripts/android-smoke.sh`,
  `Portal.Client/setup-webdriver-windows.ps1`
- CI: `.github/workflows/build.yaml`, `.github/workflows/android.yaml`
