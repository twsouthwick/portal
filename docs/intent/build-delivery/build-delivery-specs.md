# Build and Delivery Specifications

These EARS specifications distinguish observed configuration (`[x]`) from
prerelease behavior awaiting hosted-run verification (`[ ]`). A checked
requirement does not by itself imply test or code citation coverage.

- [x] **DELIVERY-001** When the web project is built, it shall type-check and bundle the frontend through its npm build integration. Evidence: `Portal.Web/Portal.Web.csproj:22-24`, `Portal.Web/package.json:1-15`.
- [x] **DELIVERY-002** When a pull request or configured branch/tag push runs the main workflow, CI shall build and test the .NET solution, validate container packaging, and lint the Helm chart. Evidence: `.github/workflows/build.yaml:1-11,164-224`.
- [x] **DELIVERY-003** When the main workflow runs on supported desktop runners, it shall build and smoke-test the configured Linux and Windows native-client targets. Evidence: `.github/workflows/build.yaml:14-162`.
- [x] **DELIVERY-004** When the Android workflow runs, it shall build an x86_64 debug APK, install it in an emulator, and run smoke/UI checks. Evidence: `.github/workflows/android.yaml:1-91`.
- [x] **DELIVERY-005** When a successful push meets the workflow's publishing conditions, CI shall publish the versioned container image and Helm chart to GHCR. Evidence: `.github/workflows/build.yaml:223-300`.
- [x] **DELIVERY-006** When native-client UI tests run, the shared suite shall capture screenshots and diagnostics, and configured CI jobs shall upload those artifacts. Evidence: `Portal.Client/tests/client.test.js:24-93`, `.github/workflows/build.yaml:96-102,156-162`, `.github/workflows/android.yaml:85-91`.
- [x] **DELIVERY-007** When developers use the documented devcontainer, it shall install the pinned .NET SDK, Node.js, Aspire CLI, and Kubernetes/container tooling. Evidence: `.devcontainer/Dockerfile:1-42`, `.devcontainer/devcontainer.json:1-29`, `global.json:1-5`.
- [ ] **DELIVERY-008** When the main build, Android debug, and native release workflows have all succeeded for the same push to `main`, the publisher shall create at most one prerelease tagged `dev-<full commit SHA>` for that commit.
- [ ] **DELIVERY-009** When a workflow is triggered by a pull request, tag push, or manual dispatch, it shall not publish a prerelease or provide release-write credentials to that build.
- [ ] **DELIVERY-010** When publishing a native prerelease, the publisher shall include the verified Linux and Windows artifacts for both architectures from one native workflow run and attempt, along with checksums and commit, run, and toolchain provenance.
- [ ] **DELIVERY-011** If publication is interrupted, the publisher shall leave the release as a draft and only retry assets from the same native run and attempt; it shall not modify an existing published release or a conflicting draft or tag.
- [ ] **DELIVERY-012** While Android distribution signing and device ABI verification are unavailable, the publisher shall exclude Android APKs and document the limitation rather than presenting smoke-signed or unsigned APKs as installable releases.
