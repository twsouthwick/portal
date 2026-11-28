# Arrow: Identity and Access

OIDC sign-in, role mapping, and authorization for portal routes and site
content.

## Status

**MAPPED** — first reconstructed 2026-11-27. Existing authentication behavior
is represented by EARS requirements; owner review and coherence audit remain.
The client manifest endpoint and Development-only OpenAPI document (issue #7)
implement AUTH-008 to AUTH-019, using the issue #6 contract merged in PR #54.
Native OIDC discovery implements AUTH-020 to AUTH-022. Local Authelia
public-client interoperability was checked manually, not by an automated test.

## References

### HLD
- `docs/high-level-design.md` — Approach, System Design, Open Questions

### LLD
- `docs/intent/identity-and-access/identity-and-access-design.md`

### EARS
- `docs/intent/identity-and-access/identity-and-access-specs.md` — 21 implemented, 1 non-want

### Tests
- `Portal.Web.Tests/AuthenticationTests.cs`
- `Portal.Web.Tests/SiteContentTests.cs`
- `Portal.Web.Tests/ClientManifestEndpointTests.cs`

### Code
- `Portal.Web/Program.cs`
- `Portal.Web/Services/SiteDetailsExtensions.cs`
- `Portal.Web/Services/ClientManifestEndpointExtensions.cs`
- `Portal.Web/Services/NativeOidcMetadataEndpointExtensions.cs`
- `Portal.Web/Components/SiteProvider.razor`
- `Portal.AppHost/AppHost.cs`
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
| Client manifest endpoint (#7) | AUTH-008, AUTH-009, AUTH-010, AUTH-011, AUTH-012, AUTH-013, AUTH-014, AUTH-015, AUTH-016 | 9 | 0 | Local Authelia access-token and native redirect interoperability lack automated integration coverage |
| Development OpenAPI (#7) | AUTH-017, AUTH-018, AUTH-019 | 2 | 0 | 1 explicit non-want |
| Native OIDC discovery (#12) | AUTH-020, AUTH-021, AUTH-022 | 3 | 0 | Local provider integration is not automated |

**Traceability:** Tests cite AUTH-001, AUTH-002, AUTH-004, AUTH-005, and
AUTH-008 through AUTH-022; direct test citations for AUTH-003 and AUTH-006
remain missing. Authelia configuration cites AUTH-007. Tests exercise JWT
signature, issuer, audience, expiry, role filtering, conditional requests,
provider failure, OpenAPI behavior, and native OIDC metadata configuration.
Provider issuance and native redirects were checked manually against local
Authelia but lack automated coverage. Private/internal flag enforcement at the
content endpoint is unresolved and is not counted as a passing requirement.

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
2. Add automated local Authelia public-client access-token and native redirect
   coverage and validate other target platforms.
3. Audit `@spec` citations and EARS coherence after implementation.

### Nice to Have
1. Add direct tests for antiforgery, path traversal, range requests, and
   missing-file responses.
