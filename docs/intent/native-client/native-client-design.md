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

**[inferred; owner review required]** A separate public OIDC client using the
system browser and authorization-code flow with PKCE S256 is preferred over a
Portal-mediated session. A native application cannot protect the confidential
`portal-web` client secret. Portal.Web uses its own `/signin-oidc` callback
and `portal.session` cookie, saves no OIDC tokens, and has no native
bearer-token API authentication. The system browser, Tauri WebView, and other
apps cannot be assumed to share cookies. The native client would not collect
passwords or store credentials in plaintext.

This choice depends on proving that the actual Authelia deployment supports a
separate public client, the platform redirects, and suitable access-token
audience and group claims. A future Portal API would need to validate issuer,
signature, expiry, audience, and token type, obtain trusted groups, and apply
the relevant role authorization. An ID token is not an API access token.
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
| Native authentication | Separate public OIDC client with system-browser code flow and PKCE S256. **[inferred; proposed, not approved or validated]** | Portal-mediated session/API flow. | Avoids embedding the confidential web secret or assuming shared browser cookies; requires provider and API compatibility. |
| UI verification | Shared Node E2E assertions use platform adapters. **[inferred]** | Separate assertions for each platform. | `Portal.Client/tests/client.test.js` shares assertions and adapters. |

## Open Questions & Future Decisions

### Deferred

1. What user need should the native client solve beyond displaying a shell?
2. Is the proposed public-client model acceptable, including its API and
   provider prerequisites, or should a Portal-mediated session be designed?
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
