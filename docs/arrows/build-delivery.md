# Arrow: Build and Delivery

Repository build, CI verification, container/chart publishing, and native
client test automation.

## Status

**MAPPED** — first reconstructed 2026-11-27. Twelve EARS requirements describe
configured and planned workflows; release-path discrepancies remain unverified.

## References

### HLD
- `docs/high-level-design.md` — Success Metrics and System Design

### LLD
- `docs/intent/build-delivery/build-delivery-design.md`

### EARS
- `docs/intent/build-delivery/build-delivery-specs.md` — 12 specs

### Tests
- Server tests invoked by `.github/workflows/build.yaml`
- Client tests: `Portal.Client/tests/`
- Prerelease policy tests: `.github/scripts/prerelease.test.mjs`
- Smoke scripts: `Portal.Client/smoke-linux.sh`,
  `Portal.Client/smoke-windows.ps1`,
  `Portal.Client/scripts/android-smoke.sh`

### Code
- `.github/workflows/build.yaml`
- `.github/workflows/android.yaml`
- `.github/workflows/native-release.yaml`
- `.github/workflows/prerelease.yaml`
- `.github/scripts/prerelease.mjs`
- `Portal.Web/Portal.Web.csproj`
- `Portal.Web/package.json`
- `Portal.Client/package.json`
- `.devcontainer/`
- `README.md`

## Architecture

**Purpose:** Verify server, chart, container, and native-client changes and
publish server artifacts on configured push triggers.

**Key components:** .NET/npm build integration; xUnit server suite; cross-OS
Tauri jobs; Android emulator workflow; container packaging validation; GHCR
image and OCI chart publication; devcontainer toolchain.

## Spec Coverage

| Category | Spec IDs | Implemented | Deferred | Gaps |
|---|---|---:|---:|---:|
| Web build, CI, client checks, publishing, devcontainer | DELIVERY-001, DELIVERY-002, DELIVERY-003, DELIVERY-004, DELIVERY-005, DELIVERY-006, DELIVERY-007 | 7 | 0 | 0 known configuration gaps |
| Native prerelease publishing | DELIVERY-008, DELIVERY-009, DELIVERY-010, DELIVERY-011, DELIVERY-012 | 0 | 5 | Implemented; pending hosted-run verification |

**Traceability:** The original seven requirements lack `@spec` annotations.
The prerelease tests and workflow cite DELIVERY-008 through DELIVERY-012.
This map does not claim the configured workflows have passed in the current
environment.

## Key Findings

1. Pull requests validate builds, tests, container packaging, and Helm lint;
   successful main/tag pushes publish GHCR artifacts
   (`.github/workflows/build.yaml:1-11,137-224,226-300`).
2. Separate workflows exercise Linux/Windows desktop and Android debug client
   targets (`.github/workflows/build.yaml:13-136`,
   `.github/workflows/android.yaml:1-91`).
3. Workflow and Android script adapter values appear inconsistent with
   `Portal.Client/tests/adapters/`; this requires execution to confirm.

## Work Required

### Must Fix
1. No workflow failure is confirmed by this mapping.

### Should Fix
1. Verify the client adapter paths in CI and Android smoke tests.
2. Reconcile stale README and `AGENTS.md` build instructions with current
   project configuration.
3. Verify GHCR publication permissions after merge; PR validation cannot
   exercise registry writes.

### Nice to Have
1. Add a machine-checkable spec citation/coherence check after the mapped
   specifications are reviewed.
