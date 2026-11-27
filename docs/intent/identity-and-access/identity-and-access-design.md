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

## Client Manifest Endpoint (planned, issue #7)

**[planned; design draft for owner review; not implemented]** The native
client needs a Portal.Web endpoint that returns the sites the authenticated
user may see. The endpoint reuses the existing provider pipeline and
authorization rule rather than introducing a parallel catalog or policy:

1. Inject `IPortalProvider` and call `GetSiteDetailsAsync` once per request.
2. Project the result through the client manifest contract from issue #6,
   retaining only sites for which `SiteInfo.IsAuthorized(ClaimsPrincipal)` is
   true (AUTH-003). This is the same role rule used by the UI and the content
   endpoint; no new rule is added.
3. Use issue #6's owner-approved contract and schema metadata without changing
   the DTO's wire format or semantics. The endpoint route and error handling
   remain part of issue #7.

**Authentication.** The owner selected the native-client public OIDC client
model (`docs/intent/native-client/native-client-design.md`). The manifest
endpoint therefore accepts an OIDC access token as a JWT bearer token through
an additional authentication scheme selected by an endpoint-specific policy.
The scheme validates issuer, signature, expiry, audience, and token type, and
maps trusted `groups` to roles exactly as the cookie scheme does. An ID token
is not accepted. Whether the Portal cookie is also accepted on this endpoint
is undecided. An unauthenticated or invalid-token request receives `401` with
RFC 9457 problem details and a bearer challenge, not the OIDC redirect used
by the Blazor routes (SITE-017). The
existing cookie/OIDC defaults, Blazor authorization, and
`/{siteId}/content/{**path}` behavior remain unchanged. Authelia support for
the separate public client, access-token audience, and group claims must be
validated before the scheme is implemented.

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
Responses use `Cache-Control: private, no-cache` and vary on `Authorization`
and `Cookie`. The endpoint is excluded from the shared output cache and keeps
no server-side representation cache, because Kubernetes-backed sites are read
live. A provider failure returns `503` with RFC 9457 problem details, never a
partial or unfiltered manifest (SITE-017).

**OpenAPI.** Issue #6 owns the manifest contract and its schema metadata.
Issue #7 will use ASP.NET Core's built-in OpenAPI support to generate a
document from that contract and the manifest endpoint's actual behavior,
including its response status codes. The document endpoint will be mapped
only in the Development environment. No Swagger or Scalar UI will be added.
This is planned, not implemented; documentation must not change the DTO's
JSON representation or weaken manifest authorization. The document endpoint's
own access policy remains to be determined.

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
| Manifest authentication | JWT bearer access token from the native public OIDC client, selected by an endpoint policy; `401` instead of redirect. **[owner-selected 2026-11-27; planned; Authelia validation pending]** | Portal-mediated session/token; cookie-only manifest. | Native apps cannot hold the web client secret or rely on a shared WebView cookie; `SaveTokens` is false. |
| Manifest site flags | Filter by roles only; `IsPrivate` and `IsInternal` are client metadata (private toggle, future home-network detection). **[owner-selected 2026-11-27; planned]** | Exclude private/internal sites server-side. | Owner direction for issue #7; optional CIDRs are separate follow-up issue #53. |
| Manifest caching | Strong per-user ETag over the filtered serialized bytes; `private, no-cache`; no shared output cache. **[planned; owner review required]** | Version counter or provider timestamp; server-side cache. | Providers expose no change token, and Kubernetes reads are live. |
| API documentation | Built-in OpenAPI document in Development only; no Swagger or Scalar UI. **[owner-selected; planned]** | Production document or interactive API UI. | The document uses #6's contract and schema metadata; #7 owns serving it. |

## Open Questions & Future Decisions

### Deferred

1. Should `IsPrivate` and `IsInternal` be enforced on direct content requests?
2. Should sign-out terminate the identity-provider session?
3. Are development users and the local-only client secret isolated from
   all non-development deployments?
4. Which authorization cases require direct component or end-to-end tests?
5. Should the manifest endpoint also accept the Portal cookie, or bearer
   tokens only?
6. Which access-token audience and claims will the Authelia public client
   issue, and does it support the native redirect URIs?
7. How should issue #53 handle an explicitly empty CIDR list or invalid
   entries?
8. What access policy should apply to the development OpenAPI document?

### Blocked

1. Manifest endpoint and OpenAPI tests and implementation use issue #6's
   contract and schema metadata merged in PR #54 and await Authelia
   public-client validation and review of the draft requirements. Issue #53 is
   a separate follow-up, not a prerequisite for #7.

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
