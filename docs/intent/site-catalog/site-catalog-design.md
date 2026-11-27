---
parent: high-level-design
prefix: SITE
---
# Site Catalog

## Context and Design Philosophy

The catalog turns site definitions from local files, ZIP archives, and
Kubernetes custom resources into a common `PortalDetails`/`SiteInfo` model.
Provider contracts are asynchronous; the composite uses registration order
for deterministic precedence. The same catalog provides metadata to the UI
and content providers to the static-content endpoint.

## Sources and Composition

The file provider reads the portal manifest and child site manifests from the
configured directory or ZIP. It attaches optional `usage.md` and `content/`
resources. If `SITE_PATH` is absent, the bundled `Portal.Web/sites` directory
is used.

The Kubernetes provider reads namespaced `PortalSite` resources from
`portal.swick.app/v1alpha1`. It is enabled when in-cluster configuration or a
kubeconfig is available; `PORTAL_KUBERNETES_NAMESPACE` selects the namespace
and defaults to `default`. It maps resource metadata into `SiteInfo` and can
load content from a referenced ConfigMap. A content key may restrict which
ConfigMap entry is exposed.

`CompositePortalProvider` uses the first provider's portal details and the
first site for a case-insensitive duplicate ID. `Program.cs` registers file
before Kubernetes, so a file entry wins a duplicate. Provider errors are not
silently treated as empty results.

## Content Endpoint

The catalog maps a site's `IFileProvider` to
`/{siteId}/content/{**path}`. The endpoint rejects empty and traversal-like
paths, requires a file, and supports range processing and MIME-type inference.
Authentication and role policy are defined by the Identity and Access segment.

## Client Manifest

The client manifest is a versioned, client-safe contract that native clients
consume. It is projected from the result of
`IPortalProvider.GetSiteDetailsAsync`, so file-, ZIP-, and Kubernetes-backed
sites reach clients through the same composite catalog as the Blazor UI.
Kubernetes `PortalSite` remains an input that `KubernetesPortalProvider` maps
into `SiteInfo`; it is not the client model. Clients never query providers or
Kubernetes directly.

### Shape

| Level | Field | Source |
|---|---|---|
| Manifest | `schemaVersion` | Constant; currently `1`. |
| Manifest | `name`, `description` | `PortalDetails.Name`, `Description` |
| Manifest | `sites` | Authorized `PortalDetails.Sites`, in provider order |
| Site | `id`, `name`, `description`, `icon`, `categories` | Same-named `SiteInfo` fields |
| Site | `url` | `SiteInfo.Url` after validation; omitted (null) when invalid or empty |
| Site | `usageInstructions` | `SiteInfo.UsageInstructions` as raw Markdown |
| Site | `isInternal`, `isPrivate` | Same-named `SiteInfo` flags |
| Site | `apps` | Valid `SiteInfo.Apps` entries |
| App | `name`, `scheme`, `storeId` | Same-named `SiteApp` fields |
| App | `store` | `SiteApp.Store` as a lowercase string, e.g. `google` |

`SiteInfo.Content`, `SiteInfo.Roles`, and provider or Kubernetes objects are
never part of the manifest. Roles remain server-side authorization input.

### Versioning

`schemaVersion` is an integer in the manifest body. Additive, optional fields
do not change it; clients ignore unknown fields. Removing or renaming a field,
or changing a field's type or meaning, increments it.

### Filtering

Only sites for which `SiteInfo.IsAuthorized(ClaimsPrincipal)` succeeds are
included, reusing the Identity and Access role rule (AUTH-003). The manifest
does not apply the web UI's private-browsing or remote-IP behavior (UX-003,
UX-004); it carries `isPrivate` and `isInternal` so the client can decide. It
also returns every valid app rather than filtering by device platform; the
client selects apps for its own OS. This keeps one representation per user,
which suits conditional requests. If no site is authorized, `sites` is empty.

### URL and protocol validation

A site `url` is kept only when it is an absolute `http` or `https` URI;
otherwise it is omitted and the site is still returned. An app is kept only
when its `scheme` is an absolute URI whose scheme is not `javascript`, `data`,
`file`, or `vbscript`; otherwise that app is omitted. `storeId` is passed
through unchanged because it may be a URL or a bare store ID (UX-008). The
allowlist of supported launch schemes and plugin handoffs is a later decision
for the service plugin contract.

### Error responses

The manifest endpoint itself is not part of this contract's implementation.
When served, failures use RFC 9457 problem details: an unauthenticated request
receives 401, and a provider failure receives 503 rather than a partial
manifest. An authenticated user with no authorized sites receives a successful
manifest with an empty `sites` list.

### Publication

The `ClientManifest`, `ClientSite`, and `ClientSiteApp` records in
`Portal.Web/Services/ClientManifest.cs` are the contract's code form. Each type
and field carries a description, and `store` is a string enumeration of
`none`, `apple`, `macos`, `google`, `microsoft`, `windows`, and `linux`, so a
generated schema documents the contract. A client-specific store enumeration
keeps the wire values independent of the internal `AppStore` model.

The manifest endpoint publishes this schema through ASP.NET Core's built-in
OpenAPI document (`AddOpenApi`, `MapOpenApi`), declaring the manifest response
and its 401 and 503 problem-details responses. The document is served only in
the Development environment, for client type generation; no Swagger or Scalar
UI is served. This design remains the statement of intent; the generated
document is its machine-readable form.

## Decisions & Alternatives

| Decision | Current approach | Alternatives considered | Rationale |
|---|---|---|---|
| Common metadata shape | Map all providers to `SiteInfo`. **[inferred]** | Provider-specific UI models. | UI consumes one model (`Portal.Web/Services/SiteInfo.cs`). |
| Duplicate resolution | Registration order, first match wins. **[inferred]** | Merge or reject duplicate IDs. | Implemented by `CompositePortalProvider`; files are registered first (`Portal.Web/Program.cs:89`). Desired precedence is unconfirmed. |
| File refresh | File manifest parsing is cached by the provider instance. **[inferred]** | Reload each request or watch for changes. | Current implementation caches its load task; README also directs operators to restart after file changes. |
| Kubernetes reads | Query resources through the Kubernetes API and fetch referenced content. **[inferred]** | Watch/cache or a separate synchronization process. | This is the current provider contract; latency, caching, and refresh expectations are not specified. |
| Client manifest source | Project from `IPortalProvider.GetSiteDetailsAsync`. | Expose `PortalSite` or a separate client catalog. | Owner-approved (issue #6): one catalog for UI and clients. |
| Client manifest versioning | Integer `schemaVersion` body field. | URL-path versioning; both. | Owner-approved (issue #6). |
| Private/internal sites in the manifest | Include with `isPrivate`/`isInternal` flags. | Exclude, or include without flags. | Owner-approved (issue #6): the client applies context-dependent behavior. |
| App platform filtering in the manifest | Return all valid apps. | Server filters by a client-supplied OS. | Owner-approved (issue #6): one cacheable representation per user. |
| Invalid URLs and schemes | Omit the invalid site URL or app; keep the site. | Reject the whole site; HTTPS-only. | Owner-approved (issue #6); the scheme allowlist is deferred to the plugin contract. |
| Manifest errors | RFC 9457 problem details; 401, 503, empty list. | Bare status codes. | Owner-approved (issue #6). |
| Client manifest publication | Built-in OpenAPI document, Development only, no UI; records carry descriptions and a string store enumeration. | Design table only; always-on or anonymous document; Swagger/Scalar UI. | Owner-approved (issue #6): enables client type generation without exposing a public API browser. |

## Open Questions & Future Decisions

### Deferred

1. Is file-over-Kubernetes precedence a required product rule?
2. Should file-backed changes be observed without restarting the application?
3. What caching, timeout, and stale-data behavior is expected for Kubernetes
   API calls?
4. What validation/error policy should apply to malformed manifests, duplicate
   ConfigMap keys, and large content payloads?
5. Should direct content resolution apply `IsPrivate`/`IsInternal` in addition
   to role checks?

## References

- HLD: `docs/high-level-design.md` — System Design
- EARS: `docs/intent/site-catalog/site-catalog-specs.md`
- Provider model and builder: `Portal.Web/Services/IPortalProvider.cs`,
  `Portal.Web/Services/PortalDetails.cs`,
  `Portal.Web/Services/SiteInfo.cs`,
  `Portal.Web/Services/PortalProviderBuilder.cs`
- File and ZIP sources: `Portal.Web/Services/FilePortalProvider.cs`,
  `Portal.Web/Services/FilePortalProviderExtensions.cs`,
  `Portal.Web/Services/ZipFileProvider.cs`,
  `Portal.Web/Services/SubdirectoryExtensions.cs`
- Kubernetes source: `Portal.Web/Services/KubernetesPortalProvider.cs`,
  `Portal.Web/Services/KubernetesPortalProviderExtensions.cs`
- Composition and content: `Portal.Web/Services/CompositePortalProvider.cs`,
  `Portal.Web/Services/SiteDetailsExtensions.cs`
- Client manifest: `Portal.Web/Services/ClientManifest.cs`,
  `Portal.Web.Tests/ClientManifestTests.cs`
- Examples and tests: `Portal.Web/sites/`,
  `Portal.Web.Tests/CompositePortalProviderTests.cs`,
  `Portal.Web.Tests/KubernetesPortalProviderTests.cs`,
  `Portal.Web.Tests/PortalProviderRegistrationTests.cs`,
  `Portal.Web.Tests/SiteContentTests.cs`
- Deployment schema: `chart/templates/crds/portalsites.portal.swick.app.yaml`
