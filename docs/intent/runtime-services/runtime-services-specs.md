# Local Runtime and Service Defaults Specifications

These brownfield EARS specifications record behavior observed in the current
implementation. `[x]` means the behavior exists; it does not mean a test
currently cites the ID. No `@spec` annotations were found in the repository.

- [x] **RUNTIME-001** When the Aspire AppHost runs, it shall start the development Keycloak resource and import the checked-in realm configuration.
- [x] **RUNTIME-002** When the Aspire topology starts the portal, it shall launch the `media` and `admin` web resources with their configured OIDC settings and Keycloak dependency.
- [x] **RUNTIME-003** When the web application registers service defaults, it shall configure OpenTelemetry, service discovery, HTTP resilience, and health checks.
- [x] **RUNTIME-004** When the application runs in Development, it shall map the configured health and alive endpoints.
- [x] **RUNTIME-005** When the web project is built, it shall target .NET 11 using the SDK version pinned in `global.json`.
- [x] **RUNTIME-006** When application data-protection keys are persisted by the current startup configuration, they shall be written beneath the process temporary-directory path.
