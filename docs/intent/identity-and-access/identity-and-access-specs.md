# Identity and Access Specifications

These brownfield EARS specifications record behavior observed in the current
implementation. `[x]` means the behavior exists; it does not mean a test
currently cites the ID. No `@spec` annotations were found in the repository.

- [x] **AUTH-001** When an unauthenticated request reaches an application endpoint covered by the fallback policy, the application shall require an authenticated identity. Evidence: `Portal.Web/Program.cs:67-73`; test: `Portal.Web.Tests/AuthenticationTests.cs:23-48`.
- [x] **AUTH-002** When OIDC authentication is configured, the application shall use the authorization-code flow with PKCE and map the `groups` claim to role claims. Evidence: `Portal.Web/Program.cs:36-65`; test: `Portal.Web.Tests/AuthenticationTests.cs:52-60`.
- [x] **AUTH-003** When a site has no required roles, the site-role policy shall allow any authenticated principal; when it has required roles, the policy shall allow a principal with at least one matching role. Evidence: `Portal.Web/Services/SiteDetailsExtensions.cs:34-68`, `Portal.Web/Components/SiteProvider.razor:61-77`.
- [x] **AUTH-004** When an authenticated principal requests site content, the content endpoint shall resolve the site and enforce that site's role requirements before serving a file. Evidence: `Portal.Web/Services/SiteDetailsExtensions.cs:70-101`; test: `Portal.Web.Tests/SiteContentTests.cs:21-63`.
- [x] **AUTH-005** When a content request refers to an unknown site, invalid path, or missing file, the endpoint shall not return the requested file. Evidence: `Portal.Web/Services/SiteDetailsExtensions.cs:72-109`.
- [x] **AUTH-006** When an authenticated user submits the logout POST, the application shall validate antiforgery, clear its local cookie, and redirect to the signed-out route. Evidence: `Portal.Web/Program.cs:116-122`, `Portal.Web/Components/Layout/MainLayout.razor:18-35`; test: `Portal.Web.Tests/AuthenticationTests.cs:37-48`.
