# Arrow: Native Client

Tauri desktop/mobile shell and its platform-adapted smoke and UI verification.

## Status

**MAPPED** — first reconstructed 2026-11-27. The current shell is a tested
placeholder; portal integration is not implemented. Approved MinVer versioning
and signed multi-ABI Android packaging are implemented; hosted-run verification
remains pending. Manual Portal URL onboarding (issue #15) checks and saves an
untrusted origin without authenticating. Optional well-known discovery (issue
#16) is implemented in the Rust host; UI end-to-end coverage remains pending.
The owner selected the public-client authentication design on
2026-11-27; real-provider and platform validation remain.

## References

### HLD
- `docs/high-level-design.md` — Approach and System Design

### LLD
- `docs/intent/native-client/native-client-design.md`

### EARS
- `docs/intent/native-client/native-client-specs.md` — 26 specs

### Tests
- `Portal.Client/tests/client.test.js`
- `Portal.Client/tests/onboarding.test.js`
- `Portal.Client/tests/android-uiautomator2.test.js`
- `Portal.Client/tests/android-webview.test.js`
- `Portal.Client/smoke-linux.sh`
- `Portal.Client/smoke-windows.ps1`
- `.github/scripts/native-release.test.mjs`
- `.github/scripts/android-signing.test.mjs`
- `Portal.Client/src-tauri/onboarding/src/` — unit tests (`cargo test -p portal-onboarding`)

### Code
- `Portal.Client/index.html`
- `Portal.Client/src/main.js`
- `Portal.Client/src-tauri/onboarding/`
- `Portal.Client/vite.config.ts`
- `Portal.Client/src-tauri/`
- `Portal.Client/tests/adapters/`
- `.github/workflows/build.yaml`
- `.github/workflows/android.yaml`
- `.github/workflows/native-release.yaml`
- `.github/scripts/configure-android-signing.mjs`

## Architecture

**Purpose:** Package and verify the current native placeholder shell across
desktop and Android environments.

**Key components:** Vite static page; Tauri v2 host and Rust bootstrap; shared
Node E2E suite; Linux/Windows WebDriver and Android UI/WebView adapters; CI
smoke scripts.

## Spec Coverage

| Category | Spec IDs | Implemented | Deferred | Gaps |
|---|---|---:|---:|---:|
| Shell and configured verification | CLIENT-001, CLIENT-002, CLIENT-003, CLIENT-004, CLIENT-005 | 5 | 0 | 0 known shell gaps |
| Approved release packaging | CLIENT-006, CLIENT-007 | 2 | 0 | Hosted signing and platform builds remain unverified |
| Manual Portal URL onboarding | CLIENT-008 to CLIENT-013 | 6 | 0 | UI flow lacks E2E coverage; Android private-CA trust unsupported |
| Authenticated startup and manifest cache (planned) | CLIENT-014 to CLIENT-022 | 0 | 9 | Native implementation and tests are absent; Authelia and platform validation remain unverified |
| Optional well-known discovery | CLIENT-023 to CLIENT-026 | 4 | 0 | Native UI flow lacks end-to-end coverage |

**Traceability:** Android signing, ABI, versioning, and release workflow tests
cite the approved EARS IDs. Portal connectivity and authentication are
explicitly outside the present native-client implementation. CLIENT-014 to
CLIENT-022 specify planned authenticated startup and cache behavior but have
no native implementation or test coverage; Authelia and platform validation
remain unverified. Onboarding unit tests cite CLIENT-008 to CLIENT-013 and
CLIENT-023 to CLIENT-026; the Tauri commands and UI cite the applicable IDs at
entry points.

## Key Findings

1. The Rust host registers only the Portal URL check, save, and load commands
   (`Portal.Client/src-tauri/src/lib.rs`).
2. The page shows the `Portal` heading, shell placeholder, and manual Portal
   URL check (`Portal.Client/index.html`, `Portal.Client/src/main.js`).
3. The developer guide states that integration and authentication are not
   implemented (`developer.md`, Native client).

## Work Required

### Must Fix
1. Verify signed multi-ABI APK publication on a push with configured signing
   secrets; arm64 physical-device validation remains intentionally excluded.

### Should Fix
1. Validate the selected public-client design against Authelia and the
   target platforms; the manifest API is implemented in identity-and-access
   (AUTH-008 to AUTH-016), but provider interoperability remains unvalidated.
2. Verify optional well-known discovery through the native UI on target
   platforms (CLIENT-023 to CLIENT-026).

### Nice to Have
1. Review release packaging behavior on additional physical devices after the
   distribution pipeline is verified.
