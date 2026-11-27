# Arrow: Kubernetes Deployment

Helm packaging, `PortalSite` schema, Kubernetes permissions, and container
configuration for the portal.

## Status

**MAPPED** — first reconstructed 2026-11-27. Seven EARS requirements capture the
checked-in chart; production operations are not fully specified.

## References

### HLD
- `docs/high-level-design.md` — System Design and Open Questions

### LLD
- `docs/intent/kubernetes-deployment/kubernetes-deployment-design.md`

### EARS
- `docs/intent/kubernetes-deployment/kubernetes-deployment-specs.md` — 7 specs

### Tests
- `Portal.Web.Tests/KubernetesPortalProviderTests.cs`
- `Portal.Web.Tests/PortalProviderRegistrationTests.cs`
- CI Helm lint: `.github/workflows/build.yaml`
- Chart release assertions: `.github/scripts/release-workflow.test.mjs`

### Code
- `chart/`
- `Portal.Web/Portal.Web.csproj`
- `Portal.Web/Services/KubernetesPortalProvider.cs`

## Architecture

**Purpose:** Install the Portal web service and configure its namespaced
Kubernetes catalog integration.

**Key components:** Helm chart metadata and values; Deployment and Service;
ServiceAccount and Role; `PortalSite` CRD; optional example ConfigMap and
resource; web container port 8081.

## Spec Coverage

| Category | Spec IDs | Implemented | Deferred | Gaps |
|---|---|---:|---:|---:|
| Chart, runtime values, RBAC, CRD, examples | K8S-001, K8S-002, K8S-003, K8S-004, K8S-005, K8S-006 | 6 | 0 | 0 known chart gaps |
| Approved release image pinning | K8S-007 | 1 | 0 | Hosted chart packaging remains unverified |

**Traceability:** The Deployment template and its release workflow test cite
K8S-007. No production cluster behavior is claimed as tested by chart lint.

## Key Findings

1. The chart deploys the application on port 8081 and defaults to one replica
   (`chart/templates/deployment.yaml:1-35`, `chart/values.yaml:1-19`).
2. RBAC is namespaced and allows `list` on PortalSites and `get` on ConfigMaps
   (`chart/templates/rbac.yaml:1-26`).
3. The CRD constrains site metadata and app/content references
   (`chart/templates/crds/portalsites.portal.swick.app.yaml:1-86`).

## Work Required

### Must Fix
1. No confirmed implementation defect is asserted by this mapping.

### Should Fix
1. Confirm probes, scale, resource, ingress, image-pull, and PortalSite watch
   requirements.
2. Reconcile the app-store CRD validation with the chart README.

### Nice to Have
1. Exercise a rendered chart against a disposable Kubernetes cluster.
