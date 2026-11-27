# Local Runtime and Service Defaults Specifications

These brownfield EARS specifications record behavior observed in the current
implementation. `[x]` means the behavior exists; it does not mean a test
currently cites the ID. Traceability is partial.

- [x] **RUNTIME-001** When the Aspire AppHost runs, it shall start the development Authelia resource with the checked-in configuration, mounted TLS certificate and signing key, secret parameters, and an HTTPS health check. Evidence: `Portal.AppHost/AutheliaResource.cs`, `Portal.AppHost/Authelia/`; the topology was checked manually, not by an automated AppHost test.
- [x] **RUNTIME-002** When the Aspire topology starts the portal, it shall launch the `media` and `admin` web resources with the same OIDC issuer and client settings and wait for Authelia to become healthy. Evidence: `Portal.AppHost/AppHost.cs`; the topology was checked manually, not by an automated AppHost test.
- [x] **RUNTIME-003** When the web application registers service defaults, it shall configure OpenTelemetry, service discovery, HTTP resilience, and health checks. Evidence: `Portal.ServiceDefaults/Extensions.cs:20-47,74-129`.
- [x] **RUNTIME-004** When the application runs in Development, it shall map the configured health and alive endpoints. Evidence: `Portal.ServiceDefaults/Extensions.cs:109-129`.
- [x] **RUNTIME-005** When the web project is built, it shall target .NET 11 using the SDK version pinned in `global.json`. Evidence: `global.json:1-5`, `Portal.Web/Portal.Web.csproj:1-45`.
- [x] **RUNTIME-006** When application data-protection keys are persisted by the current startup configuration, they shall be written beneath the process temporary-directory path. Evidence: `Portal.Web/Program.cs:82-87`.
