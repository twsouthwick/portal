---
parent: high-level-design
prefix: CLIENT
---
# Native Client

## Context and Design Philosophy

`Portal.Client` is a Tauri v2 desktop/mobile shell with a Vite-served static
frontend. The UI provides a Digital Fortress-branded address check and explicit
save confirmation. The Rust layer registers only the check, save, and load
commands for that onboarding flow, backed by the Tauri-free `portal-onboarding`
crate; it has no manifest client or authentication flow.

## Shell and Build Configuration

Tauri config defines the window, frontend build/dev commands, content security
policy, bundle icons, and mobile entry point. Desktop and Android build/test
automation lives in the GitHub workflows. The shared E2E assertions use
platform adapters for desktop WebDriver and Android UI/WebView inspection.

## Current Boundary

The client does not yet authenticate, load the portal catalog, or change
server provider registration. LAN discovery, sign-in, site launch integration,
synchronization, and other server interaction are not implemented, apart from
the manual Portal URL onboarding and optional well-known discovery described
below, which do not authenticate. The owner-selected authentication design
below is not evidence of working integration.

## Manual Portal URL Onboarding (issue #15)

**[issue #15 acceptance criteria; authentication deferred]** When Portal
discovery is unavailable or the user is remote, the user enters the existing
Portal's address manually. The client treats that address as untrusted input
from the first keystroke to the saved configuration.

**Normalization.** Input is trimmed; text without `://` is treated as
`https://`. The result must be an origin: scheme, host, and optional
non-default port, with no user information, path other than `/`, query, or
fragment. The serialized origin lowercases the host, applies IDNA, and omits a
default port. HTTPS is required. Only a debug build compiled with the
`dev-http-loopback` Cargo feature also accepts `http` for `localhost`,
`127.0.0.0/8`, or `::1`; release builds ignore the feature.

**Connection check.** After the optional discovery step described below, the
Rust host, not the WebView, sends one `GET {origin}/api/manifest`, the
documented manifest endpoint (AUTH-008). It sends no credentials or cookies,
follows no redirects, and times out after ten seconds. Certificates are
validated normally against the platform trust store
plus the Mozilla root set; there is no option to bypass validation. Android
does not yet read user-installed CAs, so a private CA is currently
unsupported there. An unauthenticated Portal answers `401` with a `Bearer`
challenge; the client reports this as "Portal found; sign-in required". A
`2xx` response is reported as reachable. Other outcomes are distinct errors:
TLS (certificate or handshake), network (DNS or connection), timeout,
authentication (`401` without a bearer challenge, `403`, or `407`, typically
from a proxy), redirect (not followed; often a forward-auth proxy), and
unexpected response. A found endpoint shows reachability, not Portal identity
or trustworthiness.

**Confirmation and storage.** Only the most recent successfully checked
origin can be saved, and only after the user explicitly confirms it. Each new
check supersedes earlier checks; a probe result updates the pending origin only
if its check is still current. The Rust host keeps that pending origin in memory
and rejects a save request for any other value. The saved file `portal.json` in
the application configuration directory contains only the origin; it is
revalidated when loaded and carries no trust or authentication state. The UI
labels a saved origin as not yet verified or signed in.

**Authentication.** Criterion 4 depends on the native public-client flow
below, which has not been validated against Authelia or the target platforms.
This change therefore performs no sign-in, sends no tokens, and does not claim
a verified connection. Sign-in is added once that validation is complete.

### Onboarding presentation

**[owner-approved 2026-11-28]** Replace the placeholder page with a
responsive, branded onboarding screen. A dark, high-contrast palette,
blue-violet accents, and a decorative portal motif give the page a distinct
identity. On wider screens, the introduction and connection form sit beside
one another; on narrow screens, they stack without horizontal scrolling.

**[owner-selected 2026-11-28]** The screen uses Digital Fortress as its
user-facing name. This changes presentation copy, not the Portal service
interfaces or the native package identity. Native validation and saved-address
errors use the same name while retaining actionable error details.

The form keeps the labeled Portal URL, connection check, outcome messages,
explicit save confirmation, and saved-address status. It explains that a
reachable endpoint is not a verified identity and that saving does not sign
the user in. Editing the address clears the previous result and hides its
confirmation. Controls remain keyboard accessible, have visible focus, and
provide readable status announcements. The screen uses local assets and system
fonts and does not introduce authentication or change the URL-checking and
storage behavior.

### Version footer (owner-approved 2026-11-28)

The onboarding footer shows the native app version, read from the Tauri
package version through a host command (MinVer in release builds per
CLIENT-007; `0.1.0` in local builds). It also shows the website version.

The Rust host reads the website version from the `Portal-Version` header
(AUTH-023) of the existing `/api/manifest` probe, so no additional request is
made during a check. The header is untrusted: the host accepts it only if it
is 1–64 characters drawn from ASCII letters, digits, `.`, `+`, and `-`, and
otherwise treats it as absent. The website version is shown only after a
check finds the Portal; editing the address or a failed check clears it.

On startup with a saved origin, the host probes that saved origin's
`/api/manifest` once with the CLIENT-010 rules and without well-known
discovery. If the probe finds the Portal, the footer shows its version;
otherwise the footer silently omits the website version and the saved origin,
status message, and confirmation state are unchanged. The startup probe does
not authenticate or establish trust.

## Well-known Portal Discovery (issue #16)

**[owner-approved design; issue #16]** Each entered address remains an
untrusted origin, normalized under the existing HTTPS and development-loopback
policy. As an optional initial discovery step, the host sends an
unauthenticated `GET` to
`{entered-origin}/.well-known/digital-fortress/manifest.json`, without cookies
or redirects and with normal certificate validation. The document is JSON:

```json
{"origin":"https://portal.example.com"}
```

On `200`, `origin` is a required absolute origin and is validated with the same
scheme, host, port, userinfo, path, query, and fragment rules as manual entry.
Other document fields are ignored and do not grant trust or authorization.
The client limits the response body to 8 KiB and reports malformed,
missing-field, oversized, or invalid-origin documents as errors.

On `404` or `302`, the client checks the entered origin directly as before,
without following the redirect. This preserves direct entry when an absent
document triggers the Portal's authentication redirect. Other discovery
errors, including other redirects, TLS failures, timeouts, authentication
challenges, and unexpected responses, are reported and do not silently fall
back. Discovery and the subsequent manifest probe each use the existing
ten-second request timeout. The client does not recursively discover the
document's target.

For either a discovered or direct origin, the existing `/api/manifest` probe
determines reachability. Only a successful current check can publish the final
origin for explicit user confirmation and storage; the UI displays that
origin and does not describe it as verified or authenticated. Neither request
sends credentials or cookies. Discovery may name a different HTTPS origin,
but a certificate and the document do not prove Portal identity.

The document is supplied externally; Portal.Web does not serve it. An alias
host must serve the document at the well-known path; an alias that redirects
that path must be configured with an exception. DNS-SD/mDNS and following
redirects are outside this design.

## Approved Release Packaging

The client release follows the build-delivery channels: successful `main`
builds update one rolling `dev` prerelease, while stable `vMAJOR.MINOR.PATCH`
tags create immutable GitHub Releases. Each channel includes Linux and Windows
executables for x86_64 and ARM64, plus one signed Android APK. The application
version shown in release artifacts follows MinVer; stable tags use the
corresponding stable version.

The Android APK contains both `arm64-v8a` and `x86_64` native libraries and is
signed with the repository's `ANDROID_KEYSTORE_BASE64`,
`ANDROID_KEYSTORE_PASSWORD`, and `ANDROID_KEY_ALIAS` secrets. The key password
equals the keystore password. CI verifies the APK's ABI entries and native
library architecture; x86_64 continues to run on the API 35 emulator. The
arm64 ABI is not run on a device, and release notes must state this limitation.
The Android version name follows MinVer, while the native workflow's run
number supplies a monotonically increasing version code for APK upgrades.

Windows and Linux binaries remain unsigned. Their current platform
prerequisites and limitations must be disclosed. The signed Android APK is
not evidence that the placeholder client integrates with Portal.

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
The owner selected (2026-11-28) a fixed loopback listener on Windows and
Linux and the custom scheme `digital-fortress:/oidc-callback` on Android; see
[Implementation choices](#implementation-choices-issue-12). Custom schemes can
be claimed by other apps and cannot establish callback authenticity; PKCE,
state, and nonce bound the consequence to a failed sign-in. Validation needs to
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

### Authenticated startup and manifest cache (issue #12)

**Design draft for owner review.** This extends the owner-selected public OIDC
client flow above; it does not change Portal.Web authentication or the
bearer-only manifest endpoint.

Before sign-in, the client fetches non-secret OIDC configuration from the
selected Portal origin at `GET /.well-known/portal-native-oidc`. The response
supplies the issuer, public client ID, API audience, and requested scopes; the
issuer's standard OIDC discovery document supplies protocol endpoints. The
client validates both documents over HTTPS and requires the discovered issuer
to match the Portal-provided issuer. It does not accept issuer or endpoint
URLs from an authorization callback. This metadata endpoint is unauthenticated
because it is needed before sign-in and contains no credentials. The client
rejects metadata bodies larger than 64 KiB while streaming the response, so a
chunked response cannot bypass the limit or force the client to buffer an
unbounded body.

After sign-in, the client validates the OIDC transaction and obtains an API
access token for the configured Portal audience. The account key is the
validated issuer plus the OIDC subject (`iss`, `sub`), never an email address
or display name. The API access token is sent only to the configured Portal
origin over validated HTTPS; it is not placed in WebView storage, the manifest
cache, logs, or diagnostics. Refresh credentials, if required by the provider,
use the platform-protected credential store described above.

On restart, before using a stored refresh token, the client compares the issuer
from the provider it just discovered with the issuer bound to the stored
account. If they differ, it must not send the refresh token to either issuer;
it requires a fresh sign-in and keeps the account's cached manifest visible.
If replacing a different account's credential entry fails, the client
invalidates the previous entry before accepting a memory-only session. If
invalidation also fails, it rejects the account switch and retains the prior
active account so that it cannot be silently restored after restart.

The cache contains only the authenticated, role-filtered client manifest DTO
returned by `GET /api/manifest`, together with its ETag, account key, and
refresh timestamp. It contains no token or unfiltered provider data. It is
stored in the OS-protected application-private data area and replaced
atomically. Each account has a separate cache entry. At startup, the last
account's cached manifest is rendered immediately with a visible stale or
refreshing state while the client attempts a network refresh. Per the owner's
selected offline policy, a stale entry remains visible until a successful
refresh replaces it or the user signs out. Signing out clears that account's
cache and credentials. A different authenticated account never sees the
previous account's cache.

The client sends `If-None-Match` with that account's cached ETag. A `200`
response is parsed and validated against the manifest DTO before atomically
replacing the cache and timestamp. A `304` retains the matching cached DTO and
updates its successful refresh time; if no cache exists for the authenticated
account, the client retries once without a conditional header. Network,
provider, or token-refresh failures keep the account's previous manifest
visible and clearly marked stale; a `401` triggers at most one token refresh
and retry before reporting that sign-in is required. No partial response is
cached. A successful response for one account cannot update another account's
entry. The latest validated manifest and ETag also remain bound to the active
account in memory, even if an encrypted cache write fails, so later refreshes
can send the matching ETag and failure views can retain the latest data. This
in-memory entry is discarded on sign-out or account change.

Persistent cache data uses the platform-protected application-private storage
available to the client target; credentials remain in the separate OS
credential store and are never copied into the cache. If the required
protection cannot be established, the client reports the storage failure
instead of silently writing the manifest or credentials to an unprotected
location. Authelia public-client issuance, platform callbacks, token refresh,
and protected-storage behavior remain validation gates; this design is not
evidence that those integrations work.

#### Implementation choices (issue #12)

**[owner-selected 2026-11-28]** These choices make the draft above concrete.

**Metadata.** The Rust host fetches `/.well-known/portal-native-oidc` with
the onboarding HTTP policy: no credentials, no redirects, normal certificate
validation, and a ten-second timeout. It requires an `https` issuer, a
non-empty client ID and API audience, and an `openid` scope. Standard
discovery then comes from `{issuer}/.well-known/openid-configuration`, and its
issuer must equal the Portal-provided issuer exactly. Before refresh, the
discovered issuer must also equal the issuer stored with the account's refresh
token; a mismatch requires a fresh sign-in without transmitting that token.

**Redirects.** Linux and Windows use the fixed loopback redirect
`http://127.0.0.1:49152/oidc-callback`, matching the local Authelia registration.
The host binds that port on `127.0.0.1` only for one sign-in. It accepts a
single `GET /oidc-callback`, answers other paths with `404`, and reports an error
if the port is unavailable. Android uses the custom scheme redirect
`digital-fortress:/oidc-callback`, delivered by the Tauri deep-link plugin.
Any Android app can register that scheme. The owner accepted this risk:
PKCE, a single-use `state`, and the nonce prevent another app from redeeming
an intercepted code or injecting an unsolicited callback, but cannot prevent
a competing handler from blocking sign-in. A verified HTTPS app link was
rejected because its domain must be fixed at build time, while each
self-hosted Portal has its own domain. Sign-in times out after five minutes
and can be cancelled; a callback for a cancelled, expired, or already used
transaction is rejected. The selected Portal address cannot be changed in the
client while sign-in is pending, so a completed authorization result cannot
replace a different Portal's view. The five-minute expiry and cancellation
remain active through token exchange and the credential commit; a transaction
cancelled or expired during exchange cannot update the active account.

**Authorization.** The request uses `response_type=code`, PKCE S256, a fresh
`state`, a nonce, and the metadata scopes. It also sends
`audience={apiAudience}` because Authelia's explicit audience mode requires
the client to request it. The system browser opens the URL. The host
validates the ID token's signature against the issuer's JWKS, plus its
issuer, audience, expiry, and nonce. The account key is that token's `iss`
plus `sub`. The access token stays in memory.

**Credentials.** One credential entry per signed-in account is kept in the
platform credential store through `keyring-core` backends: Linux Secret
Service, Windows Credential Manager, and Android Keystore-protected storage.
The entry holds the Portal origin, issuer, subject, refresh token (when
issued), and a random 256-bit cache key. It never enters WebView storage,
logs, or diagnostics. If the store is unavailable, the client keeps that
session in memory only and warns that it must sign in again next time.

**Encrypted cache.** The cache for one `(origin, iss, sub)` account is
`manifest-cache/{sha256(origin, iss, sub)}.bin` in the application data
directory. It is XChaCha20-Poly1305 ciphertext under that account's cache
key, with the file name as associated data. The plaintext holds the origin,
issuer, subject, ETag, refresh timestamp, and validated manifest DTO. Writes
use a temporary file followed by a rename. At startup, the client displays
the cache only if decryption succeeds and every account field matches the
credential entry. Sign-out deletes the credential entry and cache file;
without the entry's key, a leftover file is unreadable. Including the origin
in the account scope stops a different Portal that uses the same identity
provider from displaying another Portal's sites.

**Site list.** After sign-in, or when a cache is displayed, the manifest view
replaces onboarding and reuses its Digital Fortress header, dark palette, and
blue-violet accents. Onboarding's sign-in notice is replaced by a Sign in
action once an address is saved. The header shows the manifest name and description, a
refresh status, and a Sign out action. The status is one of: refreshing; up
to date with the refresh time; offline or failed, showing saved sites from
the refresh time; or sign-in required. Sites form a responsive card grid
(`repeat(auto-fill, minmax(16rem, 1fr))`, one column on narrow screens),
grouped under category headings in order of first appearance. A site with
several categories appears in each group. Sites with no categories appear
under "Other". Each card shows the site's icon, name, description, and
`Private` and `Internal` badges. Badges only describe a site; they neither
hide it nor grant access. Private toggles and home-network detection remain
planned (issue #53). The icon is a Bootstrap Icons class from the bundled
font. A class that is not `bi-` followed by lowercase letters, digits, and
hyphens falls back to `bi-globe`.

**Details.** Activating a card opens a details view with a Back action. It
shows the icon, name, badges, and description. When the site has an
`http(s)` URL, an Open site action opens it in the system browser. Usage
instructions are rendered from Markdown with `marked` and sanitized with
DOMPurify. Activating a rendered `http(s)` link opens it in the system
browser; other link schemes are inert. The existing `default-src 'self'` CSP
still blocks remote images. The apps section lists only apps for the current
platform: `linux` on Linux, `windows` or `microsoft` on Windows, and `google`
on Android. Each app has an Open action for its launch URI, rejecting
`javascript`, `data`, `file`, and `vbscript` as the server does. When its
store ID is an `https` URL, the app also has a Get app action. All opening
goes through the Rust host's opener, never WebView navigation.

## Decisions & Alternatives

| Decision | Current approach | Alternatives considered | Rationale |
|---|---|---|---|
| Native framework | Tauri v2 with a Vite frontend and minimal Rust bootstrap. **[inferred]** | Browser-only application or another native shell. | These are the checked-in crate and configuration. |
| Current client scope | Placeholder shell only. **[inferred from developer guide]** | Portal-connected client. | The developer guide documents the current non-integration. |
| Native authentication | Separate public OIDC client with system-browser code flow and PKCE S256. **[owner-selected 2026-11-27; not validated]** | Portal-mediated session/API flow. | Avoids embedding the confidential web secret or assuming shared browser cookies; requires provider and API compatibility. |
| Native OIDC configuration | Fetch non-secret issuer, public client ID, API audience, and scopes from the Portal's unauthenticated `/.well-known/portal-native-oidc` endpoint. **[owner-selected source; endpoint contract proposed]** | Manual issuer/client ID fields; build-time configuration. | Supports different Portal deployments while keeping the Portal URL as the user's sole endpoint selection. |
| Manifest cache | Cache only the API's filtered DTO per validated `(iss, sub)`; show it as stale until successful refresh or sign-out. **[issue #12; owner-selected offline policy 2026-11-27]** | No offline cache; time-limited stale display; account-independent cache. | Preserves useful startup while preventing one authenticated account from displaying another account's manifest. |
| Sign-in redirect | Fixed `http://127.0.0.1:49152/oidc-callback` on desktop; `digital-fortress:/oidc-callback` custom scheme on Android. **[owner-selected 2026-11-28; desktop path updated 2026-11-28]** | Ephemeral loopback port; verified HTTPS app link; loopback on Android. | Matches the Authelia registration; app links need a build-time domain that self-hosted Portals cannot share. |
| Cache protection | XChaCha20-Poly1305 file encrypted with a per-account key held in the platform credential store. **[owner-selected 2026-11-28]** | Owner-only file permissions. | Uses the credential store's protection on every target and makes sign-out cryptographically discard the cache. |
| Site list presentation | Category-grouped card grid with Private/Internal badges and a details view rendering sanitized Markdown and current-platform apps. **[owner-selected 2026-11-28]** | Flat grid with filters; compact list; direct launch; hiding private sites. | Mirrors the web portal's catalog while keeping launch decisions explicit. |
| Manual Portal URL check | Rust host optionally resolves an externally supplied well-known document, then probes `/api/manifest` without credentials or redirects; the confirmed origin is saved untrusted. **[issues #15 and #16]** | Probe from the WebView; follow redirects; save on entry. | Keeps the network policy out of the WebView CSP, avoids leaking requests to redirect targets, and preserves confirmation and no-trust criteria. |
| Website version source | `Portal-Version` header on the existing manifest probe; startup re-probes the saved origin. **[owner-selected 2026-11-28]** | Anonymous `/api/version` endpoint; field in the well-known document. | No extra request during a check; works without a well-known document. |
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
3. Should iOS become an additional release target?
4. Desktop installers, signing, production packaging, and portal integration
   are not part of the current release scope.

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
  and `.github/workflows/native-release.yaml`
- Release signing configuration: `.github/scripts/configure-android-signing.mjs`
- Release tests: `.github/scripts/native-release.test.mjs`,
  `.github/scripts/android-signing.test.mjs`
