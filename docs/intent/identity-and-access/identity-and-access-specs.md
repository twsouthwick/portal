# Identity and Access Specifications

These brownfield EARS specifications record behavior observed in the current
implementation. `[x]` means the behavior exists; it does not mean a test
currently cites the ID. Traceability is partial.

- [x] **AUTH-001** When an unauthenticated request reaches an application endpoint covered by the fallback policy, the application shall require an authenticated identity. Evidence: `Portal.Web/Program.cs:67-73`; test: `Portal.Web.Tests/AuthenticationTests.cs:23-48`.
- [x] **AUTH-002** When OIDC authentication is configured, the application shall use the authorization-code flow with PKCE and map the `groups` claim to role claims. Evidence: `Portal.Web/Program.cs:36-65`; test: `Portal.Web.Tests/AuthenticationTests.cs:52-60`.
- [x] **AUTH-003** When a site has no required roles, the site-role policy shall allow any authenticated principal; when it has required roles, the policy shall allow a principal with at least one matching role. Evidence: `Portal.Web/Services/SiteDetailsExtensions.cs:34-68`, `Portal.Web/Components/SiteProvider.razor:61-77`.
- [x] **AUTH-004** When an authenticated principal requests site content, the content endpoint shall resolve the site and enforce that site's role requirements before serving a file. Evidence: `Portal.Web/Services/SiteDetailsExtensions.cs:70-101`; test: `Portal.Web.Tests/SiteContentTests.cs:21-63`.
- [x] **AUTH-005** When a content request refers to an unknown site, invalid path, or missing file, the endpoint shall not return the requested file. Evidence: `Portal.Web/Services/SiteDetailsExtensions.cs:72-109`.
- [x] **AUTH-006** When an authenticated user submits the logout POST, the application shall validate antiforgery, clear its local cookie, and redirect to the signed-out route. Evidence: `Portal.Web/Program.cs:116-122`, `Portal.Web/Components/Layout/MainLayout.razor:25-31`.
- [x] **AUTH-007** When the local Authelia client issues a Portal ID token, it shall include the user's `groups` and `name` claims. Evidence: `Portal.AppHost/Authelia/configuration.yml`; representative users: `Portal.AppHost/Authelia/users.yml`. Token claim handling is tested in `Portal.Web.Tests/AuthenticationTests.cs`; real provider issuance was checked manually, not by an automated integration test.

## Planned: client manifest endpoint (issue #7)

Draft requirements for owner review. They are `[ ]` (specified, not
implemented) and use the issue #6 client manifest contract and schema metadata
merged in PR #54. Tests and implementation await Authelia public-client
validation and review of these requirements; issue #7 owns the endpoint route
and error handling.

- [ ] **AUTH-008** When a request to the client manifest endpoint lacks a valid access token, the endpoint shall return `401` with RFC 9457 problem details and a bearer challenge and shall not redirect to the identity provider (SITE-017).
- [ ] **AUTH-009** When the manifest endpoint receives a bearer token, it shall accept it only if its issuer, signature, expiry, audience, and token type are valid, and shall map its `groups` claim to roles.
- [ ] **AUTH-010** When an authenticated principal requests the manifest, the endpoint shall derive sites from a single `IPortalProvider.GetSiteDetailsAsync` result and include only sites for which `SiteInfo.IsAuthorized(ClaimsPrincipal)` is true.
- [ ] **AUTH-011** When the manifest includes an authorized site, the endpoint shall include its `IsPrivate` and `IsInternal` values as metadata rather than excluding the site for those flags.
- [ ] **AUTH-012** When the manifest endpoint returns a representation, it shall include a strong ETag computed from that principal's filtered serialized representation.
- [ ] **AUTH-013** When a manifest request's `If-None-Match` matches the ETag of the principal's current filtered representation, the endpoint shall return `304` without a body; otherwise it shall return `200` with the representation.
- [ ] **AUTH-014** When the manifest endpoint responds, it shall mark the response `Cache-Control: private, no-cache`, vary on `Authorization` and `Cookie`, and shall not store it in the shared output cache.
- [ ] **AUTH-015** When the provider fails while building the manifest, the endpoint shall return `503` with RFC 9457 problem details and shall not return a partial or unfiltered manifest (SITE-017).
- [ ] **AUTH-016** While the manifest endpoint is enabled, the application shall preserve AUTH-001 through AUTH-006 for Blazor routes, logout, and `/{siteId}/content/{**path}`.
- [ ] **AUTH-017** When Portal.Web runs in Development, it shall expose a generated OpenAPI document describing the manifest endpoint, including its response status codes and the issue #6 DTO schema.
- [ ] **AUTH-018** When Portal.Web runs outside Development, it shall not map the OpenAPI document endpoint.
- [D] **AUTH-019** Issue #7 shall not add a Swagger or Scalar UI.
