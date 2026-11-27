---
parent: high-level-design
prefix: AUTH
---
# Identity and Access

## Context and Design Philosophy

The portal is an authenticated application whose site metadata can carry role
requirements. Authentication, site visibility, and protected content access
are separate enforcement points. This design describes the current OIDC,
cookie, role, and content-endpoint behavior; it does not assert that every
current policy is the intended future security policy.

## Authentication

`Portal.Web/Program.cs:30-72` configures cookie authentication and OIDC
authorization-code flow with PKCE. The `groups` claim is requested and mapped
to the role claim type. The fallback authorization policy requires an
authenticated user. The application stores its local session in an HTTP-only
cookie.

The development Keycloak realm is imported by `Portal.AppHost` and configures
the development client, groups, claims, and test users. These credentials are
for local development only; their production isolation is an open operational
question.

## Authorization Boundaries

The site-provider UI filters sites against the current principal's roles.
`SiteDetailsExtensions` also checks roles when serving
`/{siteId}/content/{**path}`. The content route requires authentication,
returns not-found for an unknown site, rejects invalid paths, and does not
serve a missing file.

`IsPrivate` and `IsInternal` influence presentation/browser behavior, but the
content endpoint's role check does not evaluate these flags. Whether those
flags must restrict direct content access is unresolved. The UI is not a
security boundary.

## Sign-out

The authenticated POST `/auth/logout` endpoint validates antiforgery, clears
the Portal cookie, and redirects to `/auth/signed-out`. The identity-provider
session is not explicitly ended by the application.

## Decisions & Alternatives

| Decision | Current approach | Alternatives considered | Rationale |
|---|---|---|---|
| Identity protocol | OIDC authorization code + PKCE, with local cookie session. **[inferred]** | Anonymous access or another identity protocol. | Directly configured in `Portal.Web/Program.cs:30-65`. |
| Role source | Map the OIDC `groups` claim to application roles. **[inferred]** | Different claim, external policy service, or application roles. | The development realm emits groups and the application consumes them (`Portal.AppHost/Realms/portal-realm.json:64-75`). |
| Content authorization | Re-check the site's roles at the HTTP endpoint. **[inferred]** | Trust UI filtering alone or apply additional site flags. | The endpoint independently evaluates roles (`Portal.Web/Services/SiteDetailsExtensions.cs:70-101`). |

## Open Questions & Future Decisions

### Deferred

1. Should `IsPrivate` and `IsInternal` be enforced on direct content requests?
2. Should sign-out terminate the identity-provider session?
3. Are development users and the committed realm client secret isolated from
   all non-development deployments?
4. Which authorization cases require direct component or end-to-end tests?

## References

- HLD: `docs/high-level-design.md` — System Design and Open Questions
- EARS: `docs/intent/identity-and-access/identity-and-access-specs.md`
- Application auth: `Portal.Web/Program.cs`
- Authorization and content route: `Portal.Web/Services/SiteDetailsExtensions.cs`
- UI role filtering: `Portal.Web/Components/SiteProvider.razor`
- Local identity configuration: `Portal.AppHost/Realms/portal-realm.json`
- Tests: `Portal.Web.Tests/AuthenticationTests.cs`,
  `Portal.Web.Tests/SiteContentTests.cs`
