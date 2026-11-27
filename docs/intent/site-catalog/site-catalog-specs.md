# Site Catalog Specifications

These brownfield EARS specifications record behavior observed in the current
implementation. `[x]` means the behavior exists; it does not mean a test
currently cites the ID. No `@spec` annotations were found for this segment.

- [x] **SITE-001** When `SITE_PATH` identifies a directory or ZIP archive, the file provider shall load portal and child-site manifests from that source. Evidence: `Portal.Web/Services/FilePortalProviderExtensions.cs:5-24`, `Portal.Web/Services/FilePortalProvider.cs:7-58`, `Portal.Web/Services/ZipFileProvider.cs:1-212`.
- [x] **SITE-002** When `SITE_PATH` is not configured, the file provider shall use the bundled `Portal.Web/sites` directory. Evidence: `Portal.Web/Services/FilePortalProviderExtensions.cs:5-24`.
- [x] **SITE-003** When a file-backed site has `usage.md` or a `content/` directory, the provider shall expose those resources with the site metadata. Evidence: `Portal.Web/Services/FilePortalProvider.cs:26-58`.
- [x] **SITE-004** When multiple providers return portal details, the composite provider shall use the first provider's portal metadata and first site for each case-insensitive site ID. Evidence: `Portal.Web/Services/CompositePortalProvider.cs:10-43`, `Portal.Web/Program.cs:89`; test: `Portal.Web.Tests/CompositePortalProviderTests.cs:7-42`.
- [x] **SITE-005** When Kubernetes configuration is available, the Kubernetes provider shall list namespaced `PortalSite` resources using the configured namespace or the `default` namespace. Evidence: `Portal.Web/Services/KubernetesPortalProviderExtensions.cs:7-29`, `Portal.Web/Services/KubernetesPortalProvider.cs:30-75`.
- [x] **SITE-006** When a Kubernetes `PortalSite` is mapped, the provider shall validate and map its site metadata, roles, app links, and visibility flags into the common site model. Evidence: `Portal.Web/Services/KubernetesPortalProvider.cs:156-205`; test: `Portal.Web.Tests/KubernetesPortalProviderTests.cs:11-54`.
- [x] **SITE-007** When a Kubernetes site references ConfigMap content, the provider shall expose the referenced content and, when a key is specified, restrict the exposed content to that key. Evidence: `Portal.Web/Services/KubernetesPortalProvider.cs:121-151`; test: `Portal.Web.Tests/KubernetesPortalProviderTests.cs:56-112`.
- [x] **SITE-008** When a site content path is empty, traversal-like, missing, or a directory, the content endpoint shall not serve it as a file. Evidence: `Portal.Web/Services/SiteDetailsExtensions.cs:72-101`.
- [x] **SITE-009** When a valid content file is requested, the endpoint shall return it using file metadata for MIME type and range processing. Evidence: `Portal.Web/Services/SiteDetailsExtensions.cs:72-109`; test: `Portal.Web.Tests/SiteContentTests.cs:21-63`.
