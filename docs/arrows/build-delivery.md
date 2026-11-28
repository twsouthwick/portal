# Arrow: Build and Delivery

Repository build, CI verification, container/chart publishing, and native
client test automation.

## Status

**MAPPED** — first reconstructed 2026-11-27. Approved release behavior is
implemented in workflows and publisher code; hosted release-path verification
remains pending.

## References

### HLD
- `docs/high-level-design.md` — Success Metrics and System Design

### LLD
- `docs/intent/build-delivery/build-delivery-design.md`

### EARS
- `docs/intent/build-delivery/build-delivery-specs.md` — 20 specs

### Tests
- Server tests invoked by `.github/workflows/build.yaml`
- Client tests: `Portal.Client/tests/`
- Release workflow tests: `.github/scripts/*.test.mjs`
- Smoke scripts: `Portal.Client/smoke-linux.sh`,
  `Portal.Client/smoke-windows.ps1`,
  `Portal.Client/scripts/android-smoke.sh`

### Code
- `.github/workflows/build.yaml`
- `.github/workflows/android.yaml`
- `.github/workflows/native-release.yaml`
- `.github/workflows/prerelease.yaml`
- `.github/scripts/release-policy.mjs`
- `.github/scripts/prerelease.mjs`
- `.github/scripts/configure-android-signing.mjs`
- `chart/`
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
multi-architecture image and OCI chart publication; stable and rolling native
releases; devcontainer toolchain.

## Spec Coverage

| Category | Spec IDs | Implemented | Deferred | Gaps |
|---|---|---:|---:|---:|
| Web build, CI, client checks, publishing, devcontainer | DELIVERY-001, DELIVERY-002, DELIVERY-003, DELIVERY-004, DELIVERY-005, DELIVERY-006, DELIVERY-007 | 7 | 0 | 0 known configuration gaps |
| Approved release channels and packaging | DELIVERY-008, DELIVERY-009, DELIVERY-010, DELIVERY-011, DELIVERY-012, DELIVERY-013, DELIVERY-014, DELIVERY-015, DELIVERY-016, DELIVERY-017, DELIVERY-018 | 11 | 0 | Hosted publishing remains unverified |
| Generated Android signing compatibility | DELIVERY-019 | 1 | 0 | Hosted APK build remains unverified |
| Semantic-version release titles | DELIVERY-020 | 1 | 0 | Hosted publishing remains unverified |

**Traceability:** Release policy, publisher, workflow, Android signing, and
Helm default tests cite their EARS IDs. Hosted GHCR, GitHub Release, and
Android signing execution remain unverified.
The signing adapter and regression tests cite DELIVERY-019 and cover generated
scripts with and without pre-existing Java imports.

## Key Findings

1. Pull requests validate builds, tests, multi-architecture container
   packaging, and Helm lint; main and stable-tag pushes publish GHCR artifacts
   (`.github/workflows/build.yaml`).
2. Separate workflows exercise Linux/Windows desktop and Android debug client
   targets (`.github/workflows/build.yaml:13-136`,
   `.github/workflows/android.yaml:1-91`).
3. Workflow and Android script adapter values appear inconsistent with
   `Portal.Client/tests/adapters/`; this requires execution to confirm.

## Work Required

### Must Fix
1. Verify the first GHCR and native release publication after merge, including
   Android signing secrets and stable-tag channel selection.

### Should Fix
1. Verify the client adapter paths in CI and Android smoke tests.
2. Verify GHCR package permissions after merge; PR validation cannot exercise
   registry writes.

### Nice to Have
1. Add a machine-checkable spec citation/coherence check after the mapped
   specifications are reviewed.
