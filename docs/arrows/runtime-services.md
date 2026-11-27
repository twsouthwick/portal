# Arrow: Local Runtime and Service Defaults

Application startup, Aspire development topology, and shared runtime defaults.

## Status

**MAPPED** — first reconstructed 2026-11-27. Six EARS requirements capture
configured local runtime behavior; production key persistence and health policy
remain open.

## References

### HLD
- `docs/high-level-design.md` — System Design

### LLD
- `docs/intent/runtime-services/runtime-services-design.md`

### EARS
- `docs/intent/runtime-services/runtime-services-specs.md` — 6 specs

### Tests
- Runtime and OIDC behavior: `Portal.Web.Tests/AuthenticationTests.cs`
- No dedicated AppHost or ServiceDefaults tests were found.

### Code
- `Portal.AppHost/`
- `Portal.ServiceDefaults/`
- `Portal.Web/Program.cs`
- `Portal.Web/Portal.Web.csproj`
- `.devcontainer/`
- `global.json`
- `Portal.slnx`

## Architecture

**Purpose:** Compose Portal.Web and local identity services for development,
and provide shared observability, service-discovery, resilience, and health
defaults.

**Key components:** Aspire AppHost and Authelia configuration; two web profiles;
application startup; ServiceDefaults; SDK/solution and development-container
configuration.

## Spec Coverage

| Category | Spec IDs | Implemented | Deferred | Gaps |
|---|---|---:|---:|---:|
| AppHost, defaults, health, SDK, key storage | RUNTIME-001, RUNTIME-002, RUNTIME-003, RUNTIME-004, RUNTIME-005, RUNTIME-006 | 6 | 0 | No automated AppHost topology coverage |

**Traceability:** AppHost entry points cite RUNTIME-001 and RUNTIME-002; the
running topology was verified manually. No automated test cites those IDs.
Other runtime requirements lack citations.

## Key Findings

1. Aspire starts Authelia and `media`/`admin` instances of the web project
   (`Portal.AppHost/AppHost.cs`, `Portal.AppHost/AutheliaResource.cs`).
2. Shared defaults configure telemetry, discovery, resilience, and
   health-check behavior (`Portal.ServiceDefaults/Extensions.cs:20-47,109-129`).
3. Data-protection keys currently use a temporary-directory path
   (`Portal.Web/Program.cs:82-87`); restart and multi-replica intent is
   unresolved.

## Work Required

### Must Fix
1. No confirmed implementation defect is asserted by this mapping.

### Should Fix
1. Reconcile `AGENTS.md` .NET 10 guidance with the pinned .NET 11 SDK and
   project targets.
2. Confirm key persistence and production health endpoint requirements.

### Nice to Have
1. Add dedicated tests for AppHost topology and shared-default endpoint policy.
