# Native Client Specifications

These brownfield EARS specifications record behavior observed in the current
implementation. `[x]` means the behavior exists; it does not mean a test
currently cites the ID. No `@spec` annotations were found in the repository.

- [x] **CLIENT-001** When the native client starts, it shall display the `Portal` heading and `Native client shell` placeholder text. Evidence: `Portal.Client/index.html:8-12`; test: `Portal.Client/tests/client.test.js:24-85`.
- [x] **CLIENT-002** When the Tauri shell creates its primary window, it shall use the configured title, dimensions, frontend URL, and content security policy. Evidence: `Portal.Client/src-tauri/tauri.conf.json:3-35`.
- [x] **CLIENT-003** When the shared client E2E suite runs with a supported adapter, it shall assert the visible placeholder content, capture diagnostics/screenshots, and clean up the launched application. Evidence: `Portal.Client/tests/client.test.js:24-93`.
- [x] **CLIENT-004** When the shared E2E suite runs on Android with the WebView adapter, it shall target the Tauri WebView DOM rather than an unrelated browser target. Evidence: `Portal.Client/tests/adapters/android-webview.js:1-120`; test: `Portal.Client/tests/android-webview.test.js:1-77`.
- [x] **CLIENT-005** When the desktop or Android CI workflow runs, it shall build and smoke-test the configured debug client target. Evidence: `.github/workflows/build.yaml:53-65,138-154`, `.github/workflows/android.yaml:49-87`.
