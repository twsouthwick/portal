# Arrow: Kubernetes Deployment

Helm packaging, `PortalSite` schema, Kubernetes permissions, and container
configuration for the portal.

## Status

**MAPPED** — first reconstructed 2026-11-27. Eight EARS requirements capture the
checked-in chart; production operations are not fully specified.
The owner-approved cluster-wide permissions and removal of namespace filtering
(K8S-002, K8S-003) are implemented and checked with rendered-chart tests.
Optional workload references and GET-only workload permissions
(K8S-009, K8S-010) are implemented with rendered-chart coverage; live cluster
admission and API behavior remain unverified.

## References

### HLD
- `docs/high-level-design.md` — System Design and Open Questions

### LLD
- `docs/intent/kubernetes-deployment/kubernetes-deployment-design.md`

### EARS
- `docs/intent/kubernetes-deployment/kubernetes-deployment-specs.md` — 10 specs

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

**Purpose:** Install the Portal web service and configure its cluster-wide
Kubernetes catalog integration.

**Key components:** Helm chart metadata and values; Deployment and Service;
ServiceAccount, ClusterRole, and ClusterRoleBinding; namespaced `PortalSite`
CRD; optional example ConfigMap and resource; web container port 8080.
Operator guide: `docs/deployment.md`.

## Spec Coverage

| Category | Spec IDs | Implemented | Deferred | Gaps |
|---|---|---:|---:|---:|
| Chart, runtime values, RBAC, CRD, examples | K8S-001, K8S-002, K8S-003, K8S-004, K8S-005, K8S-006 | 6 | 0 | No `SITE_PATH`, probe, or provider-toggle values |
| Approved release image pinning | K8S-007 | 0 | 1 | Template and local test exist; hosted chart packaging remains unverified |
| Reverse-proxy forwarded headers | K8S-008 | 1 | 0 | Trusts any proxy; Portal must be reachable only through the ingress |
| Workload reference schema and read permissions | K8S-009, K8S-010 | 2 | 0 | Live cluster admission and reads unverified |

**Traceability:** Templates and release workflow tests cite K8S-002, K8S-003,
K8S-007, K8S-008, K8S-009, and K8S-010. No production cluster behavior is claimed as tested by
chart lint.

## Key Findings

1. The chart deploys the application on container port 8080 (Service port
   8081) and defaults to one replica. Before 2026-11-27 it targeted 8081,
   where the image does not listen
   (`chart/templates/deployment.yaml:1-35`, `chart/values.yaml:1-19`).
2. RBAC allows cluster-wide `list` on PortalSites and `get` on ConfigMaps,
   bound to the release's service account (`chart/templates/rbac.yaml`).
3. The CRD constrains site metadata and app/content references
   (`chart/templates/crds/portalsites.portal.swick.app.yaml:1-86`).
4. Workload references support same-namespace apps/v1 Deployment, StatefulSet,
   and ReplicaSet. The chart grants only `get` on these workloads; the CRD
   rejects namespace overrides.

## Work Required

### Must Fix
1. None known after the 2026-11-27 port and forwarded-header fixes.

### Should Fix
1. Confirm probes, scale, resource, ingress, image-pull, and PortalSite watch
   requirements (LLD open questions 1, 2, 5, and 7).
2. Assess the operational implications of cluster-wide discovery and RBAC.

### Nice to Have
1. Exercise a rendered chart against a disposable Kubernetes cluster.
