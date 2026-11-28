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

The development Authelia resource configures a confidential web client with
code flow, PKCE, and `groups` and `name` in the ID token. Its local-only users
represent media-only and admin-plus-media access. The local HTTPS issuer,
certificate, secrets, redirects, and operational prerequisites are described
in `developer.md`. This client is separate from the unresolved native-client
authentication design. Production isolation remains an operational question.

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

## Client Manifest Endpoint (issue #7)

The native client uses a Portal.Web endpoint that returns the sites the
authenticated user may see. The endpoint reuses the existing provider
pipeline and authorization rule rather than introducing a parallel catalog
or policy:

1. `GET /api/manifest` injects `IPortalProvider` and calls
   `GetSiteDetailsAsync` once per request.
2. Project the result through the client manifest contract from issue #6,
   retaining only sites for which `SiteInfo.IsAuthorized(ClaimsPrincipal)` is
   true (AUTH-003). This is the same role rule used by the UI and the content
   endpoint; no new rule is added.
3. Use issue #6's owner-approved contract and schema metadata without changing
   the DTO's wire format or semantics.

**Authentication.** The owner selected the native-client public OIDC client
model (`docs/intent/native-client/native-client-design.md`). The manifest
endpoint expects an OIDC access token as a JWT bearer token through an
endpoint-specific policy; the Portal cookie is not accepted. The bearer
scheme validates issuer, signature, expiry, and the configured API audience
(`Oidc:ApiAudience`, default `portal-api`), and maps trusted `groups` to roles
exactly as the cookie scheme does. It does not explicitly distinguish access
tokens from ID tokens: an ID token is rejected only when its `aud` does not
include the configured API audience. Keep the API audience distinct from the
web client ID, and have the native client request an access token for that
audience. An unauthenticated or invalid-token request receives `401` with RFC
9457 problem details and a bearer challenge, not the OIDC redirect used by
the Blazor routes (SITE-017). The existing cookie/OIDC defaults, Blazor
authorization, and `/{siteId}/content/{**path}` behavior remain unchanged.
Local Authelia access-token issuance and native redirect interoperability
remain separate integration validation; they do not change the API's
issuer/audience validation contract.

**Private and internal sites.** The server filters the manifest by roles
only. `IsPrivate` and `IsInternal` are delivered as site metadata. The planned
client presentation uses a private-site toggle and home-network detection for
internal sites. Optional portal-level home-network CIDRs and their client-side
use are tracked separately in [issue #53](https://github.com/twsouthwick/portal/issues/53),
not in the current issue #6 DTO or issue #7 implementation. When the
field is absent, issue #53 specifies defaults of `10.0.0.0/8`,
`172.16.0.0/12`, `192.168.0.0/16`, and `fc00::/7`; loopback and link-local
ranges are excluded from the default. Matching a CIDR does not prove a device
is on the home network or grant access; server role authorization remains
authoritative. This decision covers the manifest only and does not resolve
the content-endpoint question below.

**Conditional requests.** The endpoint computes a strong ETag from the exact
serialized bytes of the requesting user's filtered representation (for
example SHA-256, encoded as a quoted string). Authentication, filtering, and
serialization happen before `If-None-Match` is evaluated, so the comparison
only ever concerns that user's representation. A matching `If-None-Match`
value (including a list or `*`) yields `304 Not Modified` with the ETag and no
body; otherwise the endpoint returns `200` with the body and its ETag.
Successful and not-modified responses use `Cache-Control: private, no-cache`
and vary on `Authorization` and `Cookie`. The endpoint is excluded from the
shared output cache and keeps no server-side representation cache, because
Kubernetes-backed sites are read live. A provider failure returns `503` with
RFC 9457 problem details, never a partial or unfiltered manifest (SITE-017).

**Version header (owner-approved 2026-11-28).** Every `/api/manifest`
response, including the anonymous `401` bearer challenge, `304`, and `503`,
carries `Portal-Version: {version}` using the same value as the web footer
(UX-012). This lets the native client display the website version during its
existing unauthenticated check without an extra request. The owner accepted
that the version is disclosed to unauthenticated callers.

**OpenAPI.** Issue #6 owns the manifest contract and its schema metadata.
Issue #7 uses ASP.NET Core's built-in OpenAPI support to generate a document
from that contract and the manifest endpoint's actual behavior, including its
response status codes and the JWT bearer access-token security scheme. The
document endpoint is mapped only in the Development environment and requires
the application's normal authenticated session. No Swagger or Scalar UI is
added. Documentation does not change the DTO's JSON representation or weaken
manifest authorization.

## Native OIDC Configuration Discovery (issue #12)

The native client needs the identity provider and public-client settings before
it can start sign-in. These values are not inferable from the Portal URL: the
OIDC authority may use a different origin, and the existing `Oidc:ClientId`
belongs to the confidential web client.

The `GET /.well-known/portal-native-oidc` endpoint is available
without authentication and returns only non-secret deployment configuration:
the configured `Oidc:Authority` as `issuer`, a separately configured
`Oidc:NativeClientId` as `clientId`, the configured API audience as
`apiAudience`, and the whitespace-separated `Oidc:NativeScopes` string as
an ordered `scopes` array. The environment variable is
`Oidc__NativeScopes`; if it is absent, empty, or whitespace-only, the scopes
default to `openid` and `groups`. The JSON fields are `issuer`,
`clientId`, `apiAudience`, and `scopes`. The client ID is public and must
identify a separate public client registered at the issuer; the endpoint
never returns the web client ID, a client secret, tokens, or redirect URIs.
The client retrieves standard OIDC protocol endpoints from the issuer's
discovery document and verifies its `issuer` exactly matches this metadata.
The metadata is read from the user's configured Portal origin over validated
HTTPS and cannot redirect the client to a new Portal origin.

If `Oidc:NativeClientId` is not configured, the endpoint is not available.
`Oidc:ClientId`, `Oidc:ClientSecret`, existing cookie/OIDC authentication, and
the bearer-only `/api/manifest` policy remain unchanged. The issuer must
separately be configured to issue access tokens with the API audience and
trusted `groups` claims; publishing the audience does not assert that the
identity provider can issue such a token.

The endpoint returns `404` when `Oidc:NativeClientId` is absent, so
deployments that have not registered a public client do not advertise native
sign-in. It sends `Cache-Control: no-cache` so deployment configuration
changes are observed on the next client startup. No CORS policy is needed for
the native Rust HTTP client. Local Authelia token issuance and Linux loopback
redirects were checked manually; other provider registrations and target
platforms remain integration-validation gates.

## Sign-out

The authenticated POST `/auth/logout` endpoint validates antiforgery, clears
the Portal cookie, and redirects to `/auth/signed-out`. The identity-provider
session is not explicitly ended by the application.

## Decisions & Alternatives

| Decision | Current approach | Alternatives considered | Rationale |
|---|---|---|---|
| Identity protocol | OIDC authorization code + PKCE, with local cookie session. **[inferred]** | Anonymous access or another identity protocol. | Directly configured in `Portal.Web/Program.cs:30-65`. |
| Role source | Map the OIDC `groups` claim to application roles. **[inferred]** | Different claim, external policy service, or application roles. | Local Authelia emits groups in the ID token and the application consumes them (`Portal.AppHost/Authelia/configuration.yml`, `Portal.Web/Program.cs`). |
| Content authorization | Re-check the site's roles at the HTTP endpoint. **[inferred]** | Trust UI filtering alone or apply additional site flags. | The endpoint independently evaluates roles (`Portal.Web/Services/SiteDetailsExtensions.cs:70-101`). |
| Manifest authentication | JWT bearer access token from the native public OIDC client, selected by an endpoint policy; API audience is configured by `Oidc:ApiAudience` (default `portal-api`); bearer-only and `401` instead of redirect. **[owner-selected 2026-11-27]** | Portal-mediated session/token; cookie-only manifest. | Native apps cannot hold the web client secret or rely on a shared WebView cookie; `SaveTokens` is false. |
| Manifest site flags | Filter by roles only; `IsPrivate` and `IsInternal` are client metadata (private toggle, future home-network detection). **[owner-selected 2026-11-27]** | Exclude private/internal sites server-side. | Owner direction for issue #7; optional CIDRs are separate follow-up issue #53. |
| Manifest caching | Strong per-user ETag over the filtered serialized bytes; `private, no-cache`; no shared output cache. **[implemented]** | Version counter or provider timestamp; server-side cache. | Providers expose no change token, and Kubernetes reads are live. |
| API documentation | Built-in OpenAPI document in Development only, protected by the normal authenticated session; no Swagger or Scalar UI. **[owner-selected]** | Production document or interactive API UI. | The document uses #6's contract and schema metadata; #7 owns serving it. |

## Open Questions & Future Decisions

### Deferred

1. Should `IsPrivate` and `IsInternal` be enforced on direct content requests?
2. Should sign-out terminate the identity-provider session?
3. Are development users and the local-only client secret isolated from
   all non-development deployments?
4. Which authorization cases require direct component or end-to-end tests?
5. Which access-token audience and claims will the Authelia public client
   issue, and does it support the native redirect URIs? Server-side audience
   validation is implemented; provider/client interoperability remains
   unvalidated.
6. How should issue #53 handle an explicitly empty CIDR list or invalid
   entries?

### Integration validation gap

No server implementation blockers remain. Local Authelia access-token
issuance and native redirect interoperability need integration validation
before native-client integration. Issue #53 is a separate follow-up, not a
prerequisite for #7.

## References

- HLD: `docs/high-level-design.md` — System Design and Open Questions
- EARS: `docs/intent/identity-and-access/identity-and-access-specs.md`
- Application auth: `Portal.Web/Program.cs`
- Authorization and content route: `Portal.Web/Services/SiteDetailsExtensions.cs`
- UI role filtering: `Portal.Web/Components/SiteProvider.razor`
- Local identity configuration: `Portal.AppHost/Authelia/`,
  `Portal.AppHost/AutheliaResource.cs`, `developer.md`
- Tests: `Portal.Web.Tests/AuthenticationTests.cs`,
  `Portal.Web.Tests/SiteContentTests.cs`
