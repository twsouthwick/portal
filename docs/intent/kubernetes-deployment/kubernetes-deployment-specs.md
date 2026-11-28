# Kubernetes Deployment Specifications

These EARS specifications distinguish observed chart and provider behavior
(`[x]`) from specified behavior not yet fully verified (`[ ]`). The approved
image-version default has template and local-test coverage; hosted packaging
remains unverified.

- [x] **K8S-001** When the Helm chart is installed, it shall create a portal Deployment and Service using the configured image and service type. Evidence: `chart/templates/deployment.yaml:1-35`, `chart/templates/service.yaml:1-11`, `chart/values.yaml:1-21`.
- [x] **K8S-002** When the chart deploys the web container, it shall configure OIDC values and container port 8080, matching the image's `ASPNETCORE_HTTP_PORTS` default, without setting `PORTAL_KUBERNETES_NAMESPACE`; the Service shall target that port. Evidence: `chart/templates/deployment.yaml`, `chart/templates/service.yaml`, `Portal.Web/Portal.Web.csproj`; test: `.github/scripts/release-workflow.test.mjs`. The previous chart targeted 8081, where nothing listened (verified 2026-11-27 by running the image).
- [x] **K8S-003** When chart RBAC is enabled, a ClusterRole and ClusterRoleBinding shall permit the release's portal service account to list PortalSites and get ConfigMaps across all namespaces; their names shall distinguish releases with the same name in different namespaces. When RBAC is disabled, the chart shall omit these permissions. Owner-approved scope; implementation: `chart/templates/rbac.yaml`; test: `.github/scripts/release-workflow.test.mjs`.
- [x] **K8S-004** When the CRD is installed, Kubernetes shall validate namespaced `PortalSite` objects against the declared metadata, app, role, visibility, and content-reference schema. Evidence: `chart/templates/crds/portalsites.portal.swick.app.yaml:1-86`.
- [x] **K8S-005** When example resources are enabled, the chart shall create an example ConfigMap and a `PortalSite` that references it. Evidence: `chart/templates/configmap.yaml:1-8`, `chart/templates/example-portalsite.yaml:1-23`.
- [x] **K8S-006** When the chart is installed with default values, it shall configure one replica and a ClusterIP service on port 8081. Evidence: `chart/templates/deployment.yaml:6,35`, `chart/values.yaml:1-21`.
- [ ] **K8S-007** When the chart is rendered without an explicit `image.tag` override, the Deployment shall use the chart's `appVersion` as its image tag. Evidence: `chart/templates/deployment.yaml`, `.github/scripts/release-workflow.test.mjs`.
- [x] **K8S-008** Where `forwardedHeaders.enabled` is true (the default), the chart shall set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so that `X-Forwarded-Proto` and `X-Forwarded-For` from the TLS-terminating proxy determine the OIDC callback scheme and client address. Evidence: `chart/templates/deployment.yaml`, `chart/values.yaml`; test: `.github/scripts/release-workflow.test.mjs`.
