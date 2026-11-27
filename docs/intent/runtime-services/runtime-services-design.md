---
parent: high-level-design
prefix: RUNTIME
---
# Local Runtime and Service Defaults

## Context and Design Philosophy

This segment covers application startup and the local Aspire topology that
supports development. `Portal.AppHost` runs the same web project in two named
profiles and starts a development identity provider. `Portal.ServiceDefaults`
provides reusable telemetry, health, discovery, and resilience registration.

## Application Composition

`Portal.Web/Program.cs` composes Blazor Interactive Server, OIDC, authorization,
browser services, portal providers, static assets, and content routes. The
project uses the .NET SDK pinned by `global.json` (11.0.100) and targets
`net11.0`.

`Portal.AppHost` starts Keycloak with the checked-in development realm, then
starts `media` and `admin` web resources with development OIDC environment
values. Both resources wait for Keycloak and expose health checks.

## Shared Defaults

The service-defaults extension configures OpenTelemetry, service discovery,
HTTP resilience, and health checks. Health and alive endpoints are mapped
only in Development. Application data-protection keys are stored under the
temporary directory in the current implementation.

## Decisions & Alternatives

| Decision | Current approach | Alternatives considered | Rationale |
|---|---|---|---|
| Local topology | Aspire starts identity plus two instances of the web project. **[inferred]** | Run each service independently or share one profile. | Directly configured in `Portal.AppHost/AppHost.cs`. |
| Service defaults | Shared telemetry/discovery/resilience/health extension is used by the app. **[inferred]** | Duplicate service setup in each project. | Centralized in `Portal.ServiceDefaults/Extensions.cs`. |
| Target framework | .NET 11 with SDK 11.0.100 pinned. **[inferred]** | Older or unpinned SDK. | The project files and `global.json` specify the current target. |

## Open Questions & Future Decisions

### Deferred

1. Should data-protection keys persist across restarts or be shared across
   replicas?
2. Should health endpoints be available in production, and what readiness
   semantics are required?
3. The root `AGENTS.md` says .NET 10 while `global.json` and project files say
   .NET 11; update the instructions after confirming the intended target.
4. The launch profiles and two Aspire resources share OIDC configuration; no
   distinct server policy between `media` and `admin` is evident.

## References

- HLD: `docs/high-level-design.md` — System Design
- EARS: `docs/intent/runtime-services/runtime-services-specs.md`
- Application composition: `Portal.Web/Program.cs`,
  `Portal.Web/Portal.Web.csproj`
- Aspire: `Portal.AppHost/AppHost.cs`,
  `Portal.AppHost/Portal.AppHost.csproj`,
  `Portal.AppHost/Realms/portal-realm.json`,
  `Portal.AppHost/appsettings.json`,
  `Portal.AppHost/appsettings.Development.json`,
  `Portal.AppHost/Properties/launchSettings.json`,
  `aspire.config.json`
- Shared defaults: `Portal.ServiceDefaults/Extensions.cs`,
  `Portal.ServiceDefaults/Portal.ServiceDefaults.csproj`
- Global build configuration: `global.json`, `Portal.slnx`,
  `Directory.Build.props`, `Directory.Build.targets`
- Development environment: `.devcontainer/`
