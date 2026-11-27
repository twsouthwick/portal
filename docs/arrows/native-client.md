# Arrow: Native Client

Tauri desktop/mobile shell and its platform-adapted smoke and UI verification.

## Status

**MAPPED** — first reconstructed 2026-11-27. The current shell is a tested
placeholder; portal integration is not implemented. A proposed authentication
design awaits owner review and real-provider/platform validation.

## References

### HLD
- `docs/high-level-design.md` — Approach and System Design

### LLD
- `docs/intent/native-client/native-client-design.md`

### EARS
- `docs/intent/native-client/native-client-specs.md` — 5 specs

### Tests
- `Portal.Client/tests/client.test.js`
- `Portal.Client/tests/android-uiautomator2.test.js`
- `Portal.Client/tests/android-webview.test.js`
- `Portal.Client/smoke-linux.sh`
- `Portal.Client/smoke-windows.ps1`

### Code
- `Portal.Client/index.html`
- `Portal.Client/vite.config.ts`
- `Portal.Client/src-tauri/`
- `Portal.Client/tests/adapters/`
- `.github/workflows/build.yaml`
- `.github/workflows/android.yaml`

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

**Traceability:** The shared tests assert placeholder content, but no
`@spec` annotations link tests or code to these IDs. Portal connectivity and
authentication are explicitly outside the present implementation.

## Key Findings

1. The Rust host only runs the default Tauri builder
   (`Portal.Client/src-tauri/src/lib.rs:1-6`).
2. The checked-in page contains only the `Portal` heading and shell placeholder
   (`Portal.Client/index.html:8-12`).
3. The developer guide states that integration and authentication are not
   implemented (`developer.md`, Native client).

## Work Required

### Must Fix
1. No placeholder-shell defect is asserted by this mapping.

### Should Fix
1. Review the proposed native authentication design and its cross-segment API
   boundary before specifying or implementing integration.

### Nice to Have
1. Define release packaging and signing verification once supported release
   targets are decided.
