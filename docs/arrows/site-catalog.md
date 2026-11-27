# Arrow: Site Catalog

Loads, composes, maps, and serves site metadata and content from file, ZIP, and
Kubernetes sources.

## Status

**MAPPED** — first reconstructed 2026-11-27. Nine EARS requirements describe
observed behavior; precedence and refresh policy remain owner decisions.

## References

### HLD
- `docs/high-level-design.md` — Approach and System Design

### LLD
- `docs/intent/site-catalog/site-catalog-design.md`

### EARS
- `docs/intent/site-catalog/site-catalog-specs.md` — 9 specs

### Tests
- `Portal.Web.Tests/CompositePortalProviderTests.cs`
- `Portal.Web.Tests/KubernetesPortalProviderTests.cs`
- `Portal.Web.Tests/PortalProviderRegistrationTests.cs`
- `Portal.Web.Tests/SiteContentTests.cs`

### Code
- `Portal.Web/Services/`
- `Portal.Web/Program.cs`
- `Portal.Web/sites/`
- `chart/templates/crds/portalsites.portal.swick.app.yaml`

## Architecture

**Purpose:** Normalize site definitions and content into the portal's common
model and supply resources to the UI and content endpoint.

**Key components:** File and ZIP providers; Kubernetes `PortalSite` and
ConfigMap provider; `CompositePortalProvider`; common `SiteInfo` model;
authenticated static-content endpoint.

## Spec Coverage

| Category | Spec IDs | Implemented | Deferred | Gaps |
|---|---|---:|---:|---:|
| File/ZIP loading, composition, Kubernetes mapping, content | SITE-001, SITE-002, SITE-003, SITE-004, SITE-005, SITE-006, SITE-007, SITE-008, SITE-009 | 9 | 0 | 0 known implementation gaps |

**Traceability:** Provider and content tests cover portions of these behaviors,
but no tests or code carry `@spec` annotations. The counts reflect source
reconstruction, not mechanically checked test coverage.

## Key Findings

1. File sources are registered before Kubernetes, and the composite keeps the
   first case-insensitive ID match (`Portal.Web/Program.cs:89`,
   `Portal.Web/Services/CompositePortalProvider.cs:10-45`).
2. Kubernetes metadata and referenced ConfigMap entries map into `SiteInfo`
   (`Portal.Web/Services/KubernetesPortalProvider.cs:42-181`).
3. File loading is cached by the provider instance; Kubernetes calls are
   performed by the provider at read time. Refresh/caching expectations are
   not specified.

## Work Required

### Must Fix
1. No confirmed implementation defect is asserted by this mapping.

### Should Fix
1. Confirm duplicate precedence, source refresh, error, and content-size
   expectations.
2. Add `@spec` citations when corresponding code or tests are next changed.

### Nice to Have
1. Add targeted tests for malformed manifests and ConfigMap key edge cases.
