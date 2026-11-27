---
parent: high-level-design
prefix: DELIVERY
---
# Build and Delivery

## Context and Design Philosophy

This segment captures the repository's developer build, automated verification,
and artifact publishing paths. It includes the root .NET solution and web
frontend build coupling, cross-platform Tauri checks, and container/chart
release workflow. The documented workflow and configured jobs are distinguished
where they disagree.

## Build and Verification

The .NET solution includes AppHost, ServiceDefaults, Web, and Web.Tests.
Building the web project invokes npm dependency installation when needed and
then the TypeScript/Webpack production build. Main CI builds and tests the
solution and lints the Helm chart.

Client workflows build and smoke-test Linux and Windows desktop targets and
Android x86_64 debug APKs. Shared UI assertions use adapters and workflows
upload diagnostic artifacts. README documents that release signing and full
portal integration are not covered.

## Publishing

The build workflow validates container packaging without publishing on pull
requests. Successful pushes to the configured default branch or version tags
publish the container image and OCI Helm chart to GHCR. The image receives
version and `latest` tags; chart packaging uses the computed version.

## Decisions & Alternatives

| Decision | Current approach | Alternatives considered | Rationale |
|---|---|---|---|
| Web frontend build | npm typecheck/Webpack runs as part of the .NET web build. **[inferred]** | Independent frontend build or committed generated output. | `Portal.Web.csproj` owns npm build targets and generated output is ignored. |
| Client verification | Platform-specific smoke and WebDriver/UI adapters share assertions. **[inferred]** | One platform or fully separate test suites. | CI and `Portal.Client/tests/` configure adapters across targets. |
| Publishing trigger | Publish only on successful pushes to branch/tag triggers. **[inferred]** | Publish PR artifacts or manual-only releases. | Workflow conditions gate GHCR writes to pushes. |

## Open Questions & Future Decisions

### Deferred

1. README's local Docker example maps port 8080 while the app and chart use
   8081.
2. README refers to `Portal.Web/TypeScript/`, while current sources are
   `Portal.Web/ts/`.
3. Root `AGENTS.md` says .NET 10 and no test projects, contrary to `global.json`,
   project files, and `Portal.Web.Tests/`.
4. Android CI covers x86_64/API 35; physical devices, other ABIs, and signed
   release packages are not verified.
5. Successful GHCR publication and package permissions are not exercised by
   pull-request validation.

## References

- HLD: `docs/high-level-design.md` — Success Metrics and System Design
- EARS: `docs/intent/build-delivery/build-delivery-specs.md`
- Solution and project builds: `Portal.slnx`, `global.json`,
  `Directory.Build.props`, `Directory.Build.targets`,
  `Portal.Web/Portal.Web.csproj`, `Portal.Web/package.json`
- CI workflows: `.github/workflows/build.yaml`,
  `.github/workflows/android.yaml`
- Client tests and scripts: `Portal.Client/tests/`,
  `Portal.Client/scripts/android-smoke.sh`,
  `Portal.Client/smoke-linux.sh`,
  `Portal.Client/smoke-windows.ps1`,
  `Portal.Client/setup-webdriver-windows.ps1`
- Developer environment and dependency updates: `.devcontainer/`,
  `renovate.json`
- User-facing build and publishing documentation: `README.md`,
  `chart/README.md`
