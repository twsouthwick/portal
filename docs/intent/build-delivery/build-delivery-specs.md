# Build and Delivery Specifications

These brownfield EARS specifications record behavior observed in the current
repository configuration. `[x]` means configured behavior exists; it does not
mean a test currently cites the ID. No `@spec` annotations were found in the
repository.

- [x] **DELIVERY-001** When the web project is built, it shall type-check and bundle the frontend through its npm build integration. Evidence: `Portal.Web/Portal.Web.csproj:22-24`, `Portal.Web/package.json:1-15`.
- [x] **DELIVERY-002** When a pull request or configured branch/tag push runs the main workflow, CI shall build and test the .NET solution, validate container packaging, and lint the Helm chart. Evidence: `.github/workflows/build.yaml:1-11,164-224`.
- [x] **DELIVERY-003** When the main workflow runs on supported desktop runners, it shall build and smoke-test the configured Linux and Windows native-client targets. Evidence: `.github/workflows/build.yaml:14-162`.
- [x] **DELIVERY-004** When the Android workflow runs, it shall build an x86_64 debug APK, install it in an emulator, and run smoke/UI checks. Evidence: `.github/workflows/android.yaml:1-91`.
- [x] **DELIVERY-005** When a successful push meets the workflow's publishing conditions, CI shall publish the versioned container image and Helm chart to GHCR. Evidence: `.github/workflows/build.yaml:223-300`.
- [x] **DELIVERY-006** When native-client UI tests run, the shared suite shall capture screenshots and diagnostics, and configured CI jobs shall upload those artifacts. Evidence: `Portal.Client/tests/client.test.js:24-93`, `.github/workflows/build.yaml:96-102,156-162`, `.github/workflows/android.yaml:85-91`.
- [x] **DELIVERY-007** When developers use the documented devcontainer, it shall install the pinned .NET SDK, Node.js, Aspire CLI, and Kubernetes/container tooling. Evidence: `.devcontainer/Dockerfile:1-42`, `.devcontainer/devcontainer.json:1-29`, `global.json:1-5`.
