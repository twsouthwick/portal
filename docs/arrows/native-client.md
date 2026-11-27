# Arrow: Native Client

Tauri desktop/mobile shell and its platform-adapted smoke and UI verification.

## Status

**MAPPED** — first reconstructed 2026-11-27. The current shell is a tested
placeholder; portal integration is not implemented. Approved MinVer versioning
and signed multi-ABI Android packaging are implemented; hosted-run verification
remains pending.

## References

### HLD
- `docs/high-level-design.md` — Approach and System Design

### LLD
- `docs/intent/native-client/native-client-design.md`

### EARS
- `docs/intent/native-client/native-client-specs.md` — 7 specs

### Tests
- `Portal.Client/tests/client.test.js`
- `Portal.Client/tests/android-uiautomator2.test.js`
- `Portal.Client/tests/android-webview.test.js`
- `Portal.Client/smoke-linux.sh`
- `Portal.Client/smoke-windows.ps1`
- `.github/scripts/native-release.test.mjs`
- `.github/scripts/android-signing.test.mjs`

### Code
- `Portal.Client/index.html`
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

**Traceability:** Android signing, ABI, versioning, and release workflow tests
cite the approved EARS IDs. Portal connectivity and authentication are
explicitly outside the present implementation.

## Key Findings

1. The Rust host only runs the default Tauri builder
   (`Portal.Client/src-tauri/src/lib.rs:1-6`).
2. The checked-in page contains only the `Portal` heading and shell placeholder
   (`Portal.Client/index.html:8-12`).
3. The developer guide states that integration and authentication are not
   implemented (`developer.md`, Native client).

## Work Required

### Must Fix
1. Verify signed multi-ABI APK publication on a push with configured signing
   secrets; arm64 physical-device validation remains intentionally excluded.

### Should Fix
1. Review the proposed native authentication design and its cross-segment API
   boundary before specifying or implementing integration.

### Nice to Have
1. Review release packaging behavior on additional physical devices after the
   distribution pipeline is verified.
