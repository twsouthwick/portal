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

The root README explicitly states that this client does not yet connect to the
portal, implement authentication, or change server provider registration.
Portal discovery, sign-in, site launch integration, synchronization, and
server interaction are not implemented and are not specified here as planned
requirements.

## Decisions & Alternatives

| Decision | Current approach | Alternatives considered | Rationale |
|---|---|---|---|
| Native framework | Tauri v2 with a Vite frontend and minimal Rust bootstrap. **[inferred]** | Browser-only application or another native shell. | These are the checked-in crate and configuration. |
| Current client scope | Placeholder shell only. **[inferred from README]** | Portal-connected client. | README explicitly documents the current non-integration. |
| UI verification | Shared Node E2E assertions use platform adapters. **[inferred]** | Separate assertions for each platform. | `Portal.Client/tests/client.test.js` shares assertions and adapters. |

## Open Questions & Future Decisions

### Deferred

1. What user need should the native client solve beyond displaying a shell?
2. Which authentication/session model and portal API should it use?
3. Which desktop and mobile targets are release requirements?
4. Release installers, signing, production packaging, and portal integration are
   not verified by the current scaffold checks.

## References

- HLD: `docs/high-level-design.md` — Approach and System Design
- EARS: `docs/intent/native-client/native-client-specs.md`
- Product scope: `README.md` — Native client scaffold
- UI and Vite: `Portal.Client/index.html`, `Portal.Client/vite.config.ts`,
  `Portal.Client/package.json`
- Tauri host: `Portal.Client/src-tauri/`
- Tests and adapters: `Portal.Client/tests/`
- Smoke/setup scripts: `Portal.Client/smoke-linux.sh`,
  `Portal.Client/smoke-windows.ps1`,
  `Portal.Client/scripts/android-smoke.sh`,
  `Portal.Client/setup-webdriver-windows.ps1`
- CI: `.github/workflows/build.yaml`, `.github/workflows/android.yaml`
