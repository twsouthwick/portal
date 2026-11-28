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
upload diagnostic artifacts. The native release workflow adds MinVer
versioning, desktop release binaries, and a signed multi-ABI Android APK on
pushes; desktop signing and full portal integration are not covered.

## Publishing

The build workflow validates multi-architecture container packaging without
publishing on pull requests. Main pushes publish a MinVer prerelease image and
chart plus the mutable `dev` image tag. Stable `vMAJOR.MINOR.PATCH` tags
publish the matching image and chart versions and the stable image aliases
`MAJOR.MINOR`, `MAJOR`, and `latest`. The chart's `appVersion` is the default
image tag.

The native publisher observes completion of the main build, Android debug,
and native release workflows. All three must have succeeded for the same
source revision before publication. Main pushes update one rolling
prerelease tagged `dev`; stable version tags create immutable GitHub Releases.
The release display name is exactly the MinVer semantic version shared by the
verified native artifacts, including any prerelease suffix; the release tag
and commit provenance remain unchanged.
Release assets include four desktop binaries and one signed dual-ABI Android
APK from a single successful native workflow run and attempt, with
architecture-qualified names, checksums, and build/run provenance.

Publishing uses a draft until every asset is uploaded. An interrupted draft
may be retried only with the same native run and attempt; a draft belonging to
another run must be inspected and removed manually before retrying. Native
builds retried after failure must rerun all jobs so that the required desktop
and Android artifacts come from the same attempt. Publisher runs are
repository-wide serialized. Desktop binaries are unsigned and their platform
prerequisites and limitations must be disclosed. The workflow implementation
and local tests are present; hosted GHCR, signing, and release execution have
not yet been verified.

## Approved Release Policy

The proposal separates rolling development artifacts from stable releases.
All OCI tags omit the leading `v` and use the exact version computed by MinVer.

- A successful push to `main` publishes the versioned MinVer prerelease image
  and chart, plus a mutable `dev` image tag. The chart's `version` and
  `appVersion` use that MinVer version, and its default image tag is
  `appVersion`. Main builds do not update `latest` or stable floating tags.
- A stable `vMAJOR.MINOR.PATCH` tag publishes the matching MinVer image and
  chart versions. The image also receives `MAJOR.MINOR`, `MAJOR`, and `latest`
  tags. Other tags, including semantic prerelease tags, do not advance these
  stable aliases. The intended first stable tag is `v0.1.0`.
- The image is published as one OCI multi-platform manifest for `linux/amd64`
  and `linux/arm64`, for both stable and development channels.
- Main pushes update one rolling native prerelease tagged `dev`; the update
  includes the four desktop artifacts and a signed Android APK. A stable tag
  creates an immutable GitHub Release with the same artifact set. Both paths
  require the build, Android debug, and native release workflows to succeed
  for the same source revision before publishing. Release assets include
  checksums and provenance.
- Android release builds use the repository secrets
  `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD`, and
  `ANDROID_KEY_ALIAS`. The key password is the same as the keystore password.
  The APK contains `arm64-v8a` and `x86_64`; CI inspects both native libraries
  and retains the x86_64 emulator smoke test. No physical-device check is
  required, and release notes must state that arm64 has not been device-tested.
  Android `versionName` follows MinVer; `versionCode` uses the native workflow
  run number so successive APK builds have increasing codes.
- Desktop binaries remain unsigned. Their existing Windows WebView2 and Linux
  runtime/AppImage limitations remain documented.

The Android signing adapter preserves the imports emitted by Tauri and adds
`import java.util.Properties` only when the generated script lacks it. Its
injected signing block uses the simple `Properties` name and reads the file
with Kotlin's `inputStream()` rather than `FileInputStream`. Duplicate imports
are ambiguous in Kotlin, and fully qualified `java.*` names do not compile
inside the Android Gradle plugin's `android {}` block, where `java` resolves
to a Gradle extension rather than the package. The signing credentials,
Android toolchain, and APK targets are unchanged.

The rolling release publisher must serialize updates and must not replace
`dev` with an older successful main run. It first uploads and verifies new
commit-qualified assets alongside the current assets, then switches the `dev`
release manifest to the new asset set and removes stale assets. If upload or
verification fails, the previous manifest and assets remain usable; if
stale-asset cleanup fails after the switch, the manifest still identifies the
current set and a retry can finish cleanup. Stable releases are immutable and
must never be modified by a development publication.

## Decisions & Alternatives

| Decision | Current approach | Alternatives considered | Rationale |
|---|---|---|---|
| Web frontend build | npm typecheck/Webpack runs as part of the .NET web build. **[inferred]** | Independent frontend build or committed generated output. | `Portal.Web.csproj` owns npm build targets and generated output is ignored. |
| Client verification | Platform-specific smoke and WebDriver/UI adapters share assertions. **[inferred]** | One platform or fully separate test suites. | CI and `Portal.Client/tests/` configure adapters across targets. |
| Publishing trigger | Publish only on successful pushes to branch/tag triggers. **[inferred]** | Publish PR artifacts or manual-only releases. | Workflow conditions gate GHCR writes to pushes. |
| Release channels | **Owner-approved:** main publishes a rolling `dev` channel; stable SemVer tags publish immutable versioned artifacts and stable container aliases. | Point `latest` at main; retain each main prerelease. | Separates deployable stable versions from CI output while keeping one discoverable development build. |

## Open Questions & Future Decisions

### Deferred

1. Resolved 2026-11-27: the image, Docker example, and chart all use container
   port 8080.
2. README refers to `Portal.Web/TypeScript/`, while current sources are
   `Portal.Web/ts/`.
3. Root `AGENTS.md` says .NET 10 and no test projects, contrary to `global.json`,
   project files, and `Portal.Web.Tests/`.
4. Android x86_64 is emulator-smoke-tested and arm64 libraries are inspected;
   no physical-device testing is required or claimed.
5. Successful GHCR publication and package permissions are not exercised by
   pull-request validation.
6. Android signing-secret availability, multi-ABI packaging, and APK update
   behavior need hosted-run verification before the first signed APK is
   published.

## References

- HLD: `docs/high-level-design.md` — Success Metrics and System Design
- EARS: `docs/intent/build-delivery/build-delivery-specs.md`
- Solution and project builds: `Portal.slnx`, `global.json`,
  `Directory.Build.props`, `Directory.Build.targets`,
  `Portal.Web/Portal.Web.csproj`, `Portal.Web/package.json`
- CI workflows: `.github/workflows/build.yaml`,
  `.github/workflows/android.yaml`, `.github/workflows/native-release.yaml`
- Release orchestration and policy: `.github/workflows/prerelease.yaml`,
  `.github/scripts/release-policy.mjs`, `.github/scripts/prerelease.mjs`,
  `.github/scripts/configure-android-signing.mjs`
- Release tests: `.github/scripts/*.test.mjs`
- Client tests and scripts: `Portal.Client/tests/`,
  `Portal.Client/scripts/android-smoke.sh`,
  `Portal.Client/smoke-linux.sh`,
  `Portal.Client/smoke-windows.ps1`,
  `Portal.Client/setup-webdriver-windows.ps1`
- Developer environment and dependency updates: `.devcontainer/`,
  `renovate.json`
- User-facing build and publishing documentation: `README.md`,
  `chart/README.md`
