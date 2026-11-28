# Site Catalog Specifications

These brownfield EARS specifications record behavior observed in the current
implementation. `[x]` means the behavior exists; it does not mean a test
currently cites the ID. Provider requirements SITE-005, SITE-007, and SITE-020
and the client-manifest behavior have `@spec` citations; other existing
provider and content requirements still lack them.

- [x] **SITE-001** When `SITE_PATH` identifies a directory or ZIP archive, the file provider shall load portal and child-site manifests from that source. Evidence: `Portal.Web/Services/FilePortalProviderExtensions.cs:5-24`, `Portal.Web/Services/FilePortalProvider.cs:7-58`, `Portal.Web/Services/ZipFileProvider.cs:1-212`.
- [x] **SITE-002** When `SITE_PATH` is not configured, the file provider shall use the bundled `Portal.Web/sites` directory. Evidence: `Portal.Web/Services/FilePortalProviderExtensions.cs:5-24`.
- [x] **SITE-003** When a file-backed site has `usage.md` or a `content/` directory, the provider shall expose those resources with the site metadata. Evidence: `Portal.Web/Services/FilePortalProvider.cs:26-58`.
- [x] **SITE-004** When multiple providers return portal details, the composite provider shall use the first provider's portal metadata and first site for each case-insensitive site ID. Evidence: `Portal.Web/Services/CompositePortalProvider.cs:10-43`, `Portal.Web/Program.cs:89`; test: `Portal.Web.Tests/CompositePortalProviderTests.cs:7-42`.
- [x] **SITE-005** When Kubernetes configuration is available, the Kubernetes provider shall list `PortalSite` resources across all namespaces for catalog loading and individual site lookup, without using `PORTAL_KUBERNETES_NAMESPACE`. Owner-approved cross-namespace scope; implementation: `Portal.Web/Services/KubernetesPortalProviderExtensions.cs`, `Portal.Web/Services/KubernetesPortalProvider.cs`; tests: `Portal.Web.Tests/KubernetesPortalProviderTests.cs`, `Portal.Web.Tests/PortalProviderRegistrationTests.cs`.
- [x] **SITE-006** When a Kubernetes `PortalSite` is mapped, the provider shall validate and map its site metadata, roles, app links, and visibility flags into the common site model. Evidence: `Portal.Web/Services/KubernetesPortalProvider.cs:156-205`; test: `Portal.Web.Tests/KubernetesPortalProviderTests.cs:11-54`.
- [x] **SITE-007** When a Kubernetes site references ConfigMap content, the provider shall read the ConfigMap in the PortalSite's `metadata.namespace`, expose its content, and restrict exposed content to the specified key when present; if the namespace is missing or empty, it shall surface an error without fetching a ConfigMap. Owner-approved design; implementation: `Portal.Web/Services/KubernetesPortalProvider.cs`; test: `Portal.Web.Tests/KubernetesPortalProviderTests.cs`.
- [x] **SITE-008** When a site content path is empty, traversal-like, missing, or a directory, the content endpoint shall not serve it as a file. Evidence: `Portal.Web/Services/SiteDetailsExtensions.cs:72-101`.
- [x] **SITE-009** When a valid content file is requested, the endpoint shall return it using file metadata for MIME type and range processing. Evidence: `Portal.Web/Services/SiteDetailsExtensions.cs:72-109`; test: `Portal.Web.Tests/SiteContentTests.cs:21-63`.
- [x] **SITE-020** When multiple Kubernetes PortalSites have the same case-insensitive site ID, including across namespaces, the provider shall reject catalog loading or lookup of that ID with an explicit duplicate-ID error rather than choosing a resource. Owner-approved preservation of duplicate handling; implementation: `Portal.Web/Services/KubernetesPortalProvider.cs`; test: `Portal.Web.Tests/KubernetesPortalProviderTests.cs`.

## Client Manifest

These requirements specify the client manifest contract approved for issue #6.
The endpoint and OpenAPI behavior are implemented in issue #7.

- [x] **SITE-010** When a client manifest is created, the projection shall derive it from the `PortalDetails` returned by `IPortalProvider.GetSiteDetailsAsync`, including the portal name, description, and sites in provider order. Evidence: `Portal.Web/Services/ClientManifest.cs`; test: `Portal.Web.Tests/ClientManifestTests.cs`.
- [x] **SITE-011** When a client manifest is created, it shall carry an integer `schemaVersion` of `1`. Evidence: `Portal.Web/Services/ClientManifest.cs`; test: `Portal.Web.Tests/ClientManifestTests.cs`.
- [x] **SITE-012** When a site is projected into a client manifest, the projection shall include only its ID, name, description, URL, icon, categories, raw usage instructions, internal and private flags, and apps with name, scheme, lowercase store, and store ID. Evidence: `Portal.Web/Services/ClientManifest.cs`; test: `Portal.Web.Tests/ClientManifestTests.cs`.
- [x] **SITE-013** The client manifest shall not serialize `SiteInfo.Content`, `SiteInfo.Roles`, provider objects, or Kubernetes `PortalSite` resources. Evidence: `Portal.Web/Services/ClientManifest.cs`; test: `Portal.Web.Tests/ClientManifestTests.cs`.
- [x] **SITE-014** When a client manifest is created for a principal, the projection shall include only sites that the principal is authorized to see under AUTH-003, and shall return an empty site list when none are authorized. Evidence: `Portal.Web/Services/ClientManifest.cs`; test: `Portal.Web.Tests/ClientManifestTests.cs`.
- [x] **SITE-015** When a site's URL is not an absolute `http` or `https` URI, the projection shall omit that URL and keep the site. Evidence: `Portal.Web/Services/ClientManifest.cs`; test: `Portal.Web.Tests/ClientManifestTests.cs`.
- [x] **SITE-016** When an app's scheme is not an absolute URI, or uses the `javascript`, `data`, `file`, or `vbscript` scheme, the projection shall omit that app. Evidence: `Portal.Web/Services/ClientManifest.cs`; test: `Portal.Web.Tests/ClientManifestTests.cs`.
- [x] **SITE-017** When the client manifest is served to an unauthenticated request, or when the provider fails, the endpoint shall return RFC 9457 problem details with status 401 or 503 respectively, and shall not return a partial manifest. Evidence: `Portal.Web/Services/ClientManifestEndpointExtensions.cs`; test: `Portal.Web.Tests/ClientManifestEndpointTests.cs`.
- [x] **SITE-018** When the client manifest schema is generated, the manifest, site, and app types shall describe every field, and `store` shall be a string enumeration of `none`, `apple`, `macos`, `google`, `microsoft`, `windows`, and `linux`. Evidence: `Portal.Web/Services/ClientManifest.cs`; test: `Portal.Web.Tests/ClientManifestTests.cs`.
- [x] **SITE-019** Where the portal runs in the Development environment, it shall publish an OpenAPI document requiring an authenticated session and describing the client manifest response and its 401 and 503 problem-details responses; outside Development it shall not serve that document, and it shall not serve a Swagger or Scalar UI. Evidence: `Portal.Web/Program.cs`; test: `Portal.Web.Tests/ClientManifestEndpointTests.cs`.

## Workload-linked Sites

Owner-approved requirements. Implementation:
`Portal.Web/Services/KubernetesSiteStatusProvider.cs`,
`Portal.Web/Services/ISiteStatusProvider.cs`,
`Portal.Web/Services/KubernetesPortalProvider.cs`,
`Portal.Web/Services/KubernetesPortalProviderExtensions.cs`.
Tests: `Portal.Web.Tests/KubernetesPortalProviderTests.cs`,
`Portal.Web.Tests/PortalProviderRegistrationTests.cs`.

- [x] **SITE-021** When a PortalSite has no `workloadRef`, the Kubernetes provider shall preserve its existing catalog and lookup behavior without reading a workload.
- [x] **SITE-022** When a PortalSite has a valid `workloadRef`, the provider shall read the named `apps/v1` Deployment, StatefulSet, or ReplicaSet in the PortalSite's namespace on each catalog load or lookup and include the site only when desired `spec.replicas` is greater than zero, treating an omitted replica count as one without considering ready replicas.
- [x] **SITE-023** When a linked workload has zero desired replicas or its named read returns HTTP 404, the provider shall omit the site from the catalog and return null for individual lookup before fetching ConfigMap content; it shall log a warning identifying the site and reference for a missing workload.
- [x] **SITE-024** When a workload reference has an unsupported kind, invalid Kubernetes DNS-subdomain name, namespace override, or missing PortalSite namespace, or its response has missing spec or invalid/negative replicas, the provider shall surface an explicit error; workload errors other than HTTP 404 shall propagate rather than be treated as unavailable, and cancellation shall propagate.
- [x] **SITE-025** When Kubernetes PortalSites have duplicate case-insensitive IDs, the provider shall reject catalog loading or lookup of that ID even when a duplicate's linked workload is unavailable.
- [x] **SITE-026** When the Kubernetes catalog or individual lookup evaluates site availability, it shall use an injectable `ISiteStatusProvider<KubernetesPortalSiteResource>` returning shared `SiteStatus.Available` or `SiteStatus.Unavailable`; the default implementation shall own workload validation and reads and propagate errors as exceptions, without adding status to the client manifest or changing other providers.
