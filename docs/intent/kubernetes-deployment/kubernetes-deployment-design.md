---
parent: high-level-design
prefix: K8S
---
# Kubernetes Deployment

## Context and Design Philosophy

The Helm chart deploys the existing Portal web container and configures the
Kubernetes API resources that feed its site catalog. It owns the namespaced
`PortalSite` CRD, service account, permissions, service, and example resources.
This is the deployment behavior represented by the checked-in chart; cluster
operations beyond it are not assumed.

## Chart and Runtime Configuration

Chart values configure the image, service type/port, OIDC authority and
existing-secret name, namespace, example resources, and RBAC. The deployment
sets OIDC environment variables and exposes
container port 8081. The chart defaults to one replica and ClusterIP.

## Approved Release Versioning

The chart's `version` and `appVersion` follow the MinVer version produced by
the release workflow. Unless a user explicitly overrides `image.tag`, the
Deployment uses `appVersion` as its image tag. Stable chart versions therefore
deploy the matching stable image by default, while main-branch prerelease
charts deploy the matching versioned development image rather than a mutable
`latest` tag.

The CRD defines namespaced PortalSite objects and a schema for site metadata,
apps, roles, visibility flags, and optional ConfigMap content references.
The owner-approved RBAC uses a ClusterRole and ClusterRoleBinding to grant the
release's service account permission to list PortalSites and get ConfigMaps
across all namespaces. Cluster-scoped permission names include the release
namespace and name so identically named releases in different namespaces do
not collide. The deployment no longer sets `PORTAL_KUBERNETES_NAMESPACE`.
Operators disabling chart RBAC must supply equivalent permissions themselves.

## Decisions & Alternatives

| Decision | Current approach | Alternatives considered | Rationale |
|---|---|---|---|
| Deployment format | Helm chart packages the web deployment and Kubernetes catalog resources. **[inferred]** | Raw manifests or an operator. | The tracked chart templates define this interface. |
| Service exposure | ClusterIP service targeting container port 8081. **[inferred]** | External service, ingress, or another port. | `values.yaml` and the service template configure this. |
| Kubernetes permissions | **Owner-approved:** cluster-wide PortalSite list and ConfigMap get, bound to the release's service account. | Restrict catalog discovery to the release namespace. | The web catalog discovers resources in every namespace and resolves content in each resource's namespace; no extra verbs are needed. |
| Secret handling | OIDC secret is supplied by a pre-existing Kubernetes Secret. **[inferred]** | Store secret material in chart values. | The deployment references secret keys rather than embedding a value. |
| Default image tag | **Owner-approved:** use chart `appVersion` unless `image.tag` is explicitly overridden. | Default to floating `latest`. | A chart installation resolves to the image version packaged by that chart, while allowing intentional overrides. |

## Open Questions & Future Decisions

### Deferred

1. Should production deployments expose probes, resource requests/limits,
   autoscaling, disruption budgets, ingress, or image-pull secrets?
2. Should RBAC include `watch` or `get` for PortalSites for the provider's
   intended update behavior?
3. The chart's CRD permits an empty app store value while the chart README
   describes a fixed set of values; which contract is intended?
4. The README's Docker example maps 8080, while application/chart settings use
   8081.

## References

- HLD: `docs/high-level-design.md` — System Design
- EARS: `docs/intent/kubernetes-deployment/kubernetes-deployment-specs.md`
- Chart definition and values: `chart/Chart.yaml`, `chart/values.yaml`,
  `chart/README.md`
- Templates: `chart/templates/`
- Web container configuration: `Portal.Web/Portal.Web.csproj`
- Kubernetes provider contract: `Portal.Web/Services/KubernetesPortalProvider.cs`
- Site provider tests: `Portal.Web.Tests/KubernetesPortalProviderTests.cs`,
  `Portal.Web.Tests/PortalProviderRegistrationTests.cs`
- Chart release test: `.github/scripts/release-workflow.test.mjs`
