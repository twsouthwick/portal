# Agent Guide

## Start Here

- Read [README.md](README.md) for product behavior, site manifests, and deployment.
- Read [developer.md](developer.md) for local setup, project boundaries, build, tests, and CI.
- Use the pinned SDK from `global.json` (currently .NET 11.0.100); CI uses Node.js 24.
- Run commands from the repository root unless a command specifies another directory.

## Build and Run

- Build and validate the full solution with `dotnet build Portal.slnx`.
- Run the Aspire development topology with `aspire run`; `Portal.AppHost/AppHost.cs` starts the `media` and `admin` web profiles.
- For frontend-only work, use `cd Portal.Web && npm run dev`. Use `npm run build` for a production bundle, `npm run watch` while iterating, and `npm run clean` to remove generated output.
- `Portal.Web/Portal.Web.csproj` runs a frontend build before every .NET build and installs npm dependencies when `node_modules` is absent. Debug uses `npm run dev`; other configurations use `npm run build`.
- Run `dotnet test Portal.slnx` when changes affect tested behavior; validate frontend changes with the relevant npm build.
- Run one test with `dotnet test Portal.Web.Tests/Portal.Web.Tests.csproj --filter 'FullyQualifiedName=Portal.Web.Tests.CompositePortalProviderTests.GetSiteDetails_CombinesProvidersAndPrefersFirstForDuplicateIds'`.
- Lint the Helm chart with `helm lint ./chart --set oidc.authority=https://identity.example.com --set oidc.existingSecret=portal-oidc`.
- Native client tests have platform-specific prerequisites; see `developer.md`. On Linux, after building the client and installing the required WebDriver tools, run `cd Portal.Client && xvfb-run -a npm run test:e2e`.

## Project Boundaries

- `Portal.AppHost` is the Aspire orchestrator and owns local resource/profile wiring.
- `Portal.ServiceDefaults` owns shared Aspire defaults such as telemetry, health checks, service discovery, and HTTP resilience.
- `Portal.Web` is the Blazor Interactive Server application. `Program.cs` is its composition root.
- `Portal.Web/Services` owns portal/site loading, Markdown conversion, and click tracking. Providers combine an optional `SITE_PATH` directory or ZIP with Kubernetes `PortalSite` resources; file entries win duplicate IDs. Bundled sites are the fallback when no provider is configured. File manifests load at startup, while Kubernetes resources are read on subsequent portal loads.
- `Portal.Web/Browser` owns browser and JavaScript interop. Razor components should use `IClientBrowserSession` instead of invoking the bundled JavaScript directly.
- `Portal.Web/ts` and `Portal.Web/css` are frontend sources. Webpack emits the ES module and CSS into generated, gitignored `Portal.Web/wwwroot/dist`; do not edit or commit that output.
- `Portal.Client` is a standalone Tauri shell with shared UI tests and platform-specific adapters; it can check and save a manually entered Portal URL (`src-tauri/onboarding`) but does not yet authenticate.

## Conventions and Pitfalls

- Follow existing Razor component and colocated `.razor.css` patterns; use the configured Fluent UI components and Bootstrap Icons rather than introducing another UI system.
- Keep nullable reference types enabled and preserve async APIs across service and browser-interop boundaries.
- Treat implementation and configuration as authoritative when documentation differs. Current frontend sources are under `Portal.Web/ts` and `Portal.Web/css`.
- OIDC configuration uses the `Oidc` section. The `groups` claim supplies portal roles, and Aspire configures local Authelia from `Portal.AppHost/Authelia`.
- Site visibility and static-content authorization are both role-sensitive; when changing manifest or provider behavior, verify both the rendered site list and mapped content endpoints.
- Keep changes scoped to the owning project and do not edit generated `artifacts`, `obj`, `node_modules`, or `wwwroot/dist` content.

## LID
- Mode: Full
- Version: 1.4.7

## Linked-Intent Development (MANDATORY)

Use the project skill `linked-intent-dev` for code changes and `arrow-maintenance` to navigate or audit maps when available; ask for either by name if it is not selected automatically. Otherwise follow [`docs/lid/workflow.md`](docs/lid/workflow.md). Intent flows downstream:

```
HLD → LLDs → EARS → Tests → Code
```

Treat design documents as the source of truth. Inspect and approve each phase before continuing; do not make a judgment call for the owner when a decision or interpretation is ambiguous. For brownfield mapping, read every in-scope source and configuration file, present distinct segmentation lenses and granularity options, and obtain approval of the final component boundaries before creating maps. Do not claim coverage for files that were not inspected.

Start map navigation at `docs/arrows/index.yaml`, then follow the selected segment's arrow document to its design, EARS specifications, tests, and implementation. Keep the arrow index and cross-links current when maps are introduced or changed. Code and tests should cite applicable EARS IDs with `@spec` annotations.

Copilot does not run LID's Claude Code `/map-codebase` command. Follow the documented manual phase gates instead; do not imply that the plugin command or its automatic stops are available in Copilot.
