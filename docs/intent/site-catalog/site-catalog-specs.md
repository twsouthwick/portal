# Site Catalog Specifications

These brownfield EARS specifications record behavior observed in the current
implementation. `[x]` means the behavior exists; it does not mean a test
currently cites the ID. No `@spec` annotations were found in the repository.

- [x] **SITE-001** When `SITE_PATH` identifies a directory or ZIP archive, the file provider shall load portal and child-site manifests from that source.
- [x] **SITE-002** When `SITE_PATH` is not configured, the file provider shall use the bundled `Portal.Web/sites` directory.
- [x] **SITE-003** When a file-backed site has `usage.md` or a `content/` directory, the provider shall expose those resources with the site metadata.
- [x] **SITE-004** When multiple providers return portal details, the composite provider shall use the first provider's portal metadata and first site for each case-insensitive site ID.
- [x] **SITE-005** When Kubernetes configuration is available, the Kubernetes provider shall list namespaced `PortalSite` resources using the configured namespace or the `default` namespace.
- [x] **SITE-006** When a Kubernetes `PortalSite` is mapped, the provider shall validate and map its site metadata, roles, app links, and visibility flags into the common site model.
- [x] **SITE-007** When a Kubernetes site references ConfigMap content, the provider shall expose the referenced content and, when a key is specified, restrict the exposed content to that key.
- [x] **SITE-008** When a site content path is empty, traversal-like, missing, or a directory, the content endpoint shall not serve it as a file.
- [x] **SITE-009** When a valid content file is requested, the endpoint shall return it using file metadata for MIME type and range processing.
