# Arrow: Native Client

Tauri desktop/mobile shell and its platform-adapted smoke and UI verification.

## Status

**MAPPED** — first reconstructed 2026-11-27. The native client checks and
saves an untrusted origin, then signs in through the system browser (issue
#12), caches the manifest encrypted, and shows a category-grouped site grid
with a details view. Real-provider, credential-store, and Android deep-link
validation remain pending. Its onboarding screen uses the Digital Fortress name.
Approved MinVer versioning and signed multi-ABI Android packaging are
implemented; hosted-run verification remains pending. Optional well-known
discovery (issue #16) is implemented in the Rust host; native UI end-to-end
coverage remains pending. The owner selected the public-client authentication design on
2026-11-27; real-provider and platform validation remain.

## References

### HLD
- `docs/high-level-design.md` — Approach and System Design

### LLD
- `docs/intent/native-client/native-client-design.md`

### EARS
- `docs/intent/native-client/native-client-specs.md` — 43 specs

### Tests
- `Portal.Client/tests/client.test.js`
- `Portal.Client/tests/onboarding.test.js`
- `Portal.Client/tests/sites.test.js`
- `Portal.Client/tests/android-uiautomator2.test.js`
- `Portal.Client/tests/android-webview.test.js`
- `Portal.Client/smoke-linux.sh`
- `Portal.Client/smoke-windows.ps1`
- `.github/scripts/native-release.test.mjs`
- `.github/scripts/android-signing.test.mjs`
- `Portal.Client/src-tauri/onboarding/src/` — unit tests (`cargo test -p portal-onboarding`)
- `Portal.Client/src-tauri/session/src/` — unit tests with a mock IdP (`cargo test -p portal-session`)

### Code
- `Portal.Client/index.html`
- `Portal.Client/src/main.js`
- `Portal.Client/src/portal.js`
- `Portal.Client/src/sites.js`
- `Portal.Client/src/style.css`
- `Portal.Client/src-tauri/onboarding/`
- `Portal.Client/src-tauri/session/`
- `Portal.AppHost/Authelia/configuration.yml` — `portal-native` redirect URIs
- `Portal.Client/vite.config.ts`
- `Portal.Client/src-tauri/`
- `Portal.Client/tests/adapters/`
- `.github/workflows/build.yaml`
- `.github/workflows/android.yaml`
- `.github/workflows/native-release.yaml`
- `.github/scripts/configure-android-signing.mjs`

## Architecture

**Purpose:** Package and verify the native client across desktop and Android:
onboarding, public-client sign-in, encrypted manifest cache, and site list.

**Key components:** Vite static page; Tauri v2 host and Rust bootstrap; shared
`portal-session` crate (OIDC/PKCE, keyring credentials, XChaCha20-Poly1305
cache); signed-in UI (`portal.js`, `sites.js`); shared Node E2E suite; Linux/Windows WebDriver and Android UI/WebView adapters; CI
smoke scripts.

## Spec Coverage

| Category | Spec IDs | Implemented | Deferred | Gaps |
|---|---|---:|---:|---:|
| Shell and configured verification | CLIENT-001, CLIENT-002, CLIENT-003, CLIENT-004, CLIENT-005 | 5 | 0 | Updated native UI E2E assertions remain unverified |
| Approved release packaging | CLIENT-006, CLIENT-007 | 2 | 0 | Hosted signing and platform builds remain unverified |
| Manual Portal URL onboarding | CLIENT-008 to CLIENT-013 | 6 | 0 | UI flow lacks E2E coverage; Android private-CA trust unsupported |
| Authenticated startup and manifest cache | CLIENT-014 to CLIENT-022, CLIENT-043 | 10 | 0 | Unit-tested against a mock IdP; Authelia and platform credential stores remain unverified |
| Optional well-known discovery | CLIENT-023 to CLIENT-026 | 4 | 0 | Native UI flow lacks end-to-end coverage |
| Onboarding presentation | CLIENT-027 to CLIENT-029 | 3 | 0 | Automated viewport, native UI, and screen-reader verification remain unverified |
| Sign-in, encrypted cache, and site list | CLIENT-030 to CLIENT-042 | 13 | 0 | Real Authelia, Android deep link/keystore, and native UI E2E remain unverified |

**Traceability:** Android signing, ABI, versioning, and release workflow tests
cite the approved EARS IDs. CLIENT-014 to CLIENT-022, CLIENT-030 to CLIENT-035,
and CLIENT-043 are cited by `portal-session` unit tests (mock IdP, mock keyring)
and the Tauri commands; CLIENT-036 to CLIENT-042 are cited by `sites.test.js`,
`portal.js`, and `external.rs`. They remain `[ ]` until validated against
Authelia and on native targets. Onboarding unit tests cite CLIENT-008 to CLIENT-013,
CLIENT-023 to CLIENT-026, and CLIENT-028 to CLIENT-029; the Tauri commands
and UI cite the applicable IDs at entry points. CLIENT-027 has been checked at
320 and 800 CSS pixels in a browser but has no automated viewport test.

## Key Findings

1. The Rust host registers onboarding commands plus session startup, refresh,
   sign-in/cancel, sign-out, and validated external opening
   (`Portal.Client/src-tauri/src/lib.rs`).
2. The Digital Fortress onboarding page presents the address check, its
   outcome, and explicit save confirmation (`Portal.Client/index.html`,
   `Portal.Client/src/main.js`, `Portal.Client/src/style.css`).
3. Without a usable OS credential store, the session falls back to memory
   only and the cache is not persisted (`credential_store()` in
   `Portal.Client/src-tauri/src/lib.rs`).

## Work Required

### Must Fix
1. Verify signed multi-ABI APK publication on a push with configured signing
   secrets; arm64 physical-device validation remains intentionally excluded.

### Should Fix
1. Validate sign-in (loopback and `digital-fortress:` redirects) against
   Authelia and the target platforms; the manifest API is implemented in identity-and-access
   (AUTH-008 to AUTH-016), but provider interoperability remains unvalidated.
2. Verify optional well-known discovery through the native UI on target
   platforms (CLIENT-023 to CLIENT-026).
3. Verify the refreshed onboarding layout and keyboard/screen-reader behavior
   on native targets (CLIENT-027 and CLIENT-028).

### Nice to Have
1. Review release packaging behavior on additional physical devices after the
   distribution pipeline is verified.
