# Arrow: Identity and Access

OIDC sign-in, role mapping, and authorization for portal routes and site
content.

## Status

**MAPPED** — first reconstructed 2026-11-27. Current behavior is represented
by seven EARS requirements; owner review and coherence audit remain. A
planned client manifest endpoint and OpenAPI documentation (issue #7) are
drafted as AUTH-008 to AUTH-019, using the issue #6 contract merged in PR #54.
Authelia public-client validation and requirements review remain.

## References

### HLD
- `docs/high-level-design.md` — Approach, System Design, Open Questions

### LLD
- `docs/intent/identity-and-access/identity-and-access-design.md`

### EARS
- `docs/intent/identity-and-access/identity-and-access-specs.md` — 7 observed, 11 planned, 1 non-want

### Tests
- `Portal.Web.Tests/AuthenticationTests.cs`
- `Portal.Web.Tests/SiteContentTests.cs`

### Code
- `Portal.Web/Program.cs`
- `Portal.Web/Services/SiteDetailsExtensions.cs`
- `Portal.Web/Components/SiteProvider.razor`
- `Portal.AppHost/Authelia/configuration.yml`
- `Portal.AppHost/Authelia/users.yml`
- `Portal.AppHost/AutheliaResource.cs`

## Architecture

**Purpose:** Authenticate portal users and apply role checks to site discovery
and protected content requests.

**Key components:** OIDC/cookie setup in `Program.cs`; role policy and content
endpoint in `SiteDetailsExtensions.cs`; role-aware UI filtering in
`SiteProvider.razor`; development identity configuration in Authelia.

## Spec Coverage

| Category | Spec IDs | Implemented | Deferred | Gaps |
|---|---|---:|---:|---:|
| Authentication, role policy, logout, content access | AUTH-001, AUTH-002, AUTH-003, AUTH-004, AUTH-005, AUTH-006 | 6 | 0 | 0 known implementation gaps |
| Local identity token claims | AUTH-007 | 1 | 0 | Provider configuration and issuance lack automated integration coverage |
| Client manifest endpoint (planned, #7) | AUTH-008, AUTH-009, AUTH-010, AUTH-011, AUTH-012, AUTH-013, AUTH-014, AUTH-015, AUTH-016 | 0 | 0 | 9 planned; Authelia validation and requirements review pending |
| Development OpenAPI (planned, #7) | AUTH-017, AUTH-018, AUTH-019 | 0 | 0 | 2 planned; 1 explicit non-want |

**Traceability:** Tests cite AUTH-001 and AUTH-002; Authelia configuration
cites AUTH-007. The other existing behavior lacks citations. Provider issuance
and role-based browser access were verified manually, not by an automated
Authelia integration test. Private/internal flag enforcement at the content
endpoint is unresolved and is not counted as a passing requirement.

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
2. After Authelia public-client validation, review AUTH-008 to AUTH-019, then
   add failing tests and implement the manifest endpoint and development
   OpenAPI using the issue #6 contract.
3. Complete and verify `@spec` citations when the remaining behaviors change.

### Nice to Have
1. Add direct tests for antiforgery, path traversal, range requests, and
   missing-file responses.
