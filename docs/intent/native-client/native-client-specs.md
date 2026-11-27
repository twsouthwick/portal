# Native Client Specifications

These brownfield EARS specifications record behavior observed in the current
implementation. `[x]` means the behavior exists; it does not mean a test
currently cites the ID. No `@spec` annotations were found in the repository.

- [x] **CLIENT-001** When the native client starts, it shall display the `Portal` heading and `Native client shell` placeholder text.
- [x] **CLIENT-002** When the Tauri shell creates its primary window, it shall use the configured title, dimensions, frontend URL, and content security policy.
- [x] **CLIENT-003** When the shared client E2E suite runs with a supported adapter, it shall assert the visible placeholder content, capture diagnostics/screenshots, and clean up the launched application.
- [x] **CLIENT-004** When the Android WebView adapter locates the application page, it shall inspect the Tauri WebView DOM rather than treating an unrelated browser target as the client.
- [x] **CLIENT-005** When the desktop or Android CI workflow runs, it shall build and smoke-test the configured debug client target.
