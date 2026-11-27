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

## Decisions & Alternatives

| Decision | Current approach | Alternatives considered | Rationale |
|---|---|---|---|
| Common metadata shape | Map all providers to `SiteInfo`. **[inferred]** | Provider-specific UI models. | UI consumes one model (`Portal.Web/Services/SiteInfo.cs`). |
| Duplicate resolution | Registration order, first match wins. **[inferred]** | Merge or reject duplicate IDs. | Implemented by `CompositePortalProvider`; files are registered first (`Program.cs:89`). Desired precedence is unconfirmed. |
| File refresh | File manifest parsing is cached by the provider instance. **[inferred]** | Reload each request or watch for changes. | Current implementation caches its load task; README also directs operators to restart after file changes. |
| Kubernetes reads | Query resources through the Kubernetes API and fetch referenced content. **[inferred]** | Watch/cache or a separate synchronization process. | This is the current provider contract; latency, caching, and refresh expectations are not specified. |

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
- Examples and tests: `Portal.Web/sites/`,
  `Portal.Web.Tests/CompositePortalProviderTests.cs`,
  `Portal.Web.Tests/KubernetesPortalProviderTests.cs`,
  `Portal.Web.Tests/PortalProviderRegistrationTests.cs`,
  `Portal.Web.Tests/SiteContentTests.cs`
- Deployment schema: `chart/templates/crds/portalsites.portal.swick.app.yaml`
