# Kubernetes Deployment Specifications

These brownfield EARS specifications record behavior observed in the current
chart and provider integration. `[x]` means configured behavior exists; it
does not mean a test currently cites the ID. No `@spec` annotations were
found in the repository.

- [x] **K8S-001** When the Helm chart is installed, it shall create a portal Deployment and Service using the configured image and service type.
- [x] **K8S-002** When the chart deploys the web container, it shall configure OIDC values, the Kubernetes namespace, and container port 8081.
- [x] **K8S-003** When chart RBAC is enabled, the portal service account shall be permitted to list namespaced `PortalSite` resources and get ConfigMaps.
- [x] **K8S-004** When the CRD is installed, Kubernetes shall validate namespaced `PortalSite` objects against the declared metadata, app, role, visibility, and content-reference schema.
- [x] **K8S-005** When example resources are enabled, the chart shall create an example ConfigMap and a `PortalSite` that references it.
- [x] **K8S-006** When the chart is installed with default values, it shall configure one replica and a ClusterIP service on port 8081.
