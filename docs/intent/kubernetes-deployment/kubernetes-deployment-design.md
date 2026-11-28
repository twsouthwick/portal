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
sets OIDC environment variables and `PORTAL_KUBERNETES_NAMESPACE` and exposes
container port 8080, the port the image's base `ASPNETCORE_HTTP_PORTS`
setting makes Kestrel listen on. The Service keeps its default port 8081 and
targets container port 8080. Unless `forwardedHeaders.enabled` is false, the
deployment sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`; behind a
TLS-terminating ingress, Portal otherwise builds an `http://` OIDC
`redirect_uri` and sees the proxy address instead of the client address. The
chart defaults to one replica and ClusterIP.

## Approved Release Versioning

The chart's `version` and `appVersion` follow the MinVer version produced by
the release workflow. Unless a user explicitly overrides `image.tag`, the
Deployment uses `appVersion` as its image tag. Stable chart versions therefore
deploy the matching stable image by default, while main-branch prerelease
charts deploy the matching versioned development image rather than a mutable
`latest` tag.

The CRD defines namespaced PortalSite objects and a schema for site metadata,
apps, roles, visibility flags, and optional ConfigMap content references.
RBAC grants the service account permission to list PortalSites and get
ConfigMaps in the release namespace.

## Decisions & Alternatives

| Decision | Current approach | Alternatives considered | Rationale |
|---|---|---|---|
| Deployment format | Helm chart packages the web deployment and Kubernetes catalog resources. **[inferred]** | Raw manifests or an operator. | The tracked chart templates define this interface. |
| Service exposure | ClusterIP service on port 8081 targeting container port 8080. **[proposed 2026-11-27]** | Override `ASPNETCORE_HTTP_PORTS` to 8081. | The image listens on 8080 by default; targeting it avoids a chart-only port override and matches the Docker example. |
| Forwarded headers | Enabled by default through `ASPNETCORE_FORWARDEDHEADERS_ENABLED`. **[proposed 2026-11-27]** | Configure explicit known proxies in code. | Portal is expected behind an ingress that terminates TLS. The ASP.NET Core switch trusts forwarded headers from any source, so Portal must be reachable only through that proxy. |
| Kubernetes permissions | Namespaced list of PortalSites and get of ConfigMaps. **[inferred]** | Cluster-wide read or watch/get permissions. | The Role is restricted to the release namespace; whether provider refresh requires additional verbs is unconfirmed. |
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
4. Resolved 2026-11-27: the image listens on 8080; the chart now targets it.
5. Should the chart support `SITE_PATH` volumes, extra environment variables,
   probes, and disabling the Kubernetes provider for file-only deployments?
   See `docs/deployment.md` for current workarounds.
6. Should `PortalSite` discovery span multiple namespaces? It currently reads
   only `PORTAL_KUBERNETES_NAMESPACE`.
7. Should the CRD and RBAC be published as versioned plain-manifest release
   assets? The CRD is installed as a regular chart template and is deleted on
   `helm uninstall`.

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
