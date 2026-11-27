# Arrow: Identity and Access

OIDC sign-in, role mapping, and authorization for portal routes and site
content.

## Status

**MAPPED** — first reconstructed 2026-11-27. Current behavior is represented
by six inferred EARS requirements; owner review and coherence audit remain.

## References

### HLD
- `docs/high-level-design.md` — Approach, System Design, Open Questions

### LLD
- `docs/intent/identity-and-access/identity-and-access-design.md`

### EARS
- `docs/intent/identity-and-access/identity-and-access-specs.md` — 6 specs

### Tests
- `Portal.Web.Tests/AuthenticationTests.cs`
- `Portal.Web.Tests/SiteContentTests.cs`

### Code
- `Portal.Web/Program.cs`
- `Portal.Web/Services/SiteDetailsExtensions.cs`
- `Portal.Web/Components/SiteProvider.razor`
- `Portal.AppHost/Realms/portal-realm.json`

## Architecture

**Purpose:** Authenticate portal users and apply role checks to site discovery
and protected content requests.

**Key components:** OIDC/cookie setup in `Program.cs`; role policy and content
endpoint in `SiteDetailsExtensions.cs`; role-aware UI filtering in
`SiteProvider.razor`; development identity configuration in the Keycloak realm.

## Spec Coverage

| Category | Spec IDs | Implemented | Deferred | Gaps |
|---|---|---:|---:|---:|
| Authentication, role policy, logout, content access | AUTH-001, AUTH-002, AUTH-003, AUTH-004, AUTH-005, AUTH-006 | 6 | 0 | 0 known implementation gaps |

**Traceability:** Relevant tests exist for role-claim mapping and role-based
content requests, but neither tests nor implementation currently carry
`@spec` annotations. Private/internal flag enforcement at the content endpoint
is unresolved and is not counted as a passing requirement.

## Key Findings

1. `Program.cs` configures OIDC code flow with PKCE and maps `groups` as roles
   (`Portal.Web/Program.cs:30-72`).
2. The content endpoint performs its own site-role check
   (`Portal.Web/Services/SiteDetailsExtensions.cs:70-101`).
3. `IsPrivate` and `IsInternal` do not participate in this endpoint's role
   check; their access implications need owner review.

## Work Required

### Must Fix
1. No confirmed implementation defect is asserted by this mapping.

### Should Fix
1. Resolve the open authorization policy for `IsPrivate` and `IsInternal`.
2. Add and verify `@spec` citations when these behaviors are next changed.

### Nice to Have
1. Add direct tests for antiforgery, path traversal, range requests, and
   missing-file responses.
