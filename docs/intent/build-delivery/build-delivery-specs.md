# Build and Delivery Specifications

These brownfield EARS specifications record behavior observed in the current
repository configuration. `[x]` means configured behavior exists; it does not
mean a test currently cites the ID. No `@spec` annotations were found in the
repository.

- [x] **DELIVERY-001** When the web project is built, it shall type-check and bundle the frontend through its npm build integration.
- [x] **DELIVERY-002** When a pull request or configured branch/tag push runs the main workflow, CI shall build and test the .NET solution, validate container packaging, and lint the Helm chart.
- [x] **DELIVERY-003** When the main workflow runs on supported desktop runners, it shall build and smoke-test the configured Linux and Windows native-client targets.
- [x] **DELIVERY-004** When the Android workflow runs, it shall build an x86_64 debug APK, install it in an emulator, and run smoke/UI checks.
- [x] **DELIVERY-005** When a successful push meets the workflow's publishing conditions, CI shall publish the versioned container image and Helm chart to GHCR.
- [x] **DELIVERY-006** When native-client tests fail or run, the configured workflow shall retain screenshots or diagnostics as artifacts where the job is configured to upload them.
- [x] **DELIVERY-007** When developers use the documented devcontainer, it shall install the pinned .NET SDK, Node.js, Aspire CLI, and Kubernetes/container tooling.
