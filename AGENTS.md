# Agent Guide

## Start Here

- Read [README.md](README.md) for product behavior, site manifests, and deployment.
- Use the pinned SDK from `global.json`; the solution targets .NET 10.
- Run commands from the repository root unless a command includes `cd Portal.Web`.

## Build and Run

- Build and validate the full solution with `dotnet build Portal.slnx`.
- Run the Aspire development topology with `aspire run`; `Portal.AppHost/AppHost.cs` starts the `media` and `admin` web profiles.
- For frontend-only work, use `cd Portal.Web && npm run dev`. Use `npm run build` for a production bundle, `npm run watch` while iterating, and `npm run clean` to remove generated output.
- `Portal.Web/Portal.Web.csproj` runs the matching npm build before every .NET build and installs npm dependencies when `node_modules` is absent.
- There are currently no test projects. Validate focused frontend changes with the relevant npm build, then run `dotnet build Portal.slnx` before finishing.

## Project Boundaries

- `Portal.AppHost` is the Aspire orchestrator and owns local resource/profile wiring.
- `Portal.ServiceDefaults` owns shared Aspire defaults such as telemetry, health checks, service discovery, and HTTP resilience.
- `Portal.Web` is the Blazor Interactive Server application. `Program.cs` is its composition root.
- `Portal.Web/Services` owns portal/site loading, Markdown conversion, and click tracking. Sites may come from a directory or ZIP selected by `SITE_PATH`; use the checked-in `Portal.Web/sites` manifests as examples.
- `Portal.Web/Browser` owns browser and JavaScript interop. Razor components should use `IClientBrowserSession` instead of invoking the bundled JavaScript directly.
- `Portal.Web/ts` and `Portal.Web/css` are frontend sources. Webpack emits the ES module and CSS into generated, gitignored `Portal.Web/wwwroot/dist`; do not edit or commit that output.

## Conventions and Pitfalls

- Follow existing Razor component and colocated `.razor.css` patterns; use the configured Fluent UI components and Bootstrap Icons rather than introducing another UI system.
- Keep nullable reference types enabled and preserve async APIs across service and browser-interop boundaries.
- Treat implementation and configuration as authoritative when documentation differs. Current frontend sources are under `Portal.Web/ts`, not the `Portal.Web/TypeScript` path shown in parts of the root README.
- OIDC configuration uses the `Oidc` section. The `groups` claim supplies portal roles, and Aspire imports the local development realm from `Portal.AppHost/Realms`.
- Site visibility and static-content authorization are both role-sensitive; when changing manifest or provider behavior, verify both the rendered site list and mapped content endpoints.
- Keep changes scoped to the owning project and do not edit generated `artifacts`, `obj`, `node_modules`, or `wwwroot/dist` content.

## LID
- Mode: Full
- Version: 1.4.7

## Linked-Intent Development (MANDATORY)

When the `linked-intent-dev` skill is available (such as in Claude Code or Cursor with plugins), use it. Otherwise follow [`docs/lid/workflow.md`](docs/lid/workflow.md) for all code changes. Intent flows downstream:

```
HLD → LLDs → EARS → Tests → Code
```

Treat design documents as the source of truth. Inspect and approve each phase before continuing; do not make a judgment call for the owner when a decision or interpretation is ambiguous. For brownfield mapping, read every in-scope source and configuration file, present distinct segmentation lenses and granularity options, and obtain approval of the final component boundaries before creating maps. Do not claim coverage for files that were not inspected.

Start map navigation at `docs/arrows/index.yaml`, then follow the selected segment's arrow document to its design, EARS specifications, tests, and implementation. Keep the arrow index and cross-links current when maps are introduced or changed. Code and tests should cite applicable EARS IDs with `@spec` annotations.
