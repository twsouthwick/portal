# Portal Helm chart

This chart installs the PortalSite CRD and optional example resources for the portal.
For versions, registry access, installing without Helm, file-backed
deployments, and migration, see the [deployment guide](../docs/deployment.md).

## Install

Create a confidential OIDC client with `https://<portal-host>/signin-oidc` as an allowed redirect URI. The ID token must include the user's portal roles in a `groups` claim.

Store the client secret in Kubernetes:

```bash
kubectl create secret generic portal-oidc --from-literal=client-secret='<secret>' -n default
```

Create a values file:

```yaml
oidc:
  authority: https://identity.example.com/realms/portal
  clientId: portal-web
  existingSecret: portal-oidc
```

The Service listens on port 8081 (`service.port`) and targets container port
8080. The chart sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so the OIDC
callback uses `https` behind a TLS-terminating ingress. Set
`forwardedHeaders.enabled=false` only when Portal is reached without a proxy.

Then install the chart:

```bash
helm install portal ./chart -n default -f values.production.yaml
```

After the CRD has been installed and registered, enable the example resources:

```bash
helm upgrade portal ./chart -n default -f values.production.yaml --set exampleResources.enabled=true
```

## PortalSite resources

The portal reads resources across all namespaces. Each resource has a
unique `spec.id` (lowercase letters, digits and hyphens, at most 63 characters)
across the cluster and a nonempty `spec.name`. Duplicate IDs are errors. Other
fields match the site manifest: `description`,
`url`, `icon`, `categories`, `roles`, `usageInstructions`, `isPrivate`,
`isInternal`, and `apps`. Each app has `name`, `scheme`, `store` (one of
`none`, `apple`, `macos`, `google`, `microsoft`, `windows`, or `linux`), and
`storeId`. An empty roles list allows any authenticated user.

```yaml
apiVersion: portal.swick.app/v1alpha1
kind: PortalSite
metadata:
  name: home
spec:
  id: home
  name: Home
  url: https://home.example.com
  roles: [admin]
  usageInstructions: |
    Open the home dashboard.
  contentRef:
    name: home-content
    key: logo.png
```

`contentRef` is optional and refers to a ConfigMap in the PortalSite's namespace. Its
`data` and `binaryData` keys are served at `/<id>/content/<key>` under the
site's role policy. `key` restricts access to one key; omit it to expose all
keys. ConfigMap keys are filenames, not nested paths. Resource and content
changes are reflected on subsequent requests; API and configuration errors
are surfaced rather than silently ignored. If `SITE_PATH` is also configured,
its entries are combined with these resources and win on duplicate site IDs.

With `rbac.create=true` (the default), the chart creates a ClusterRole and
ClusterRoleBinding granting the release's service account `list` on PortalSites
and `get` on ConfigMaps across all namespaces. Installing or upgrading requires
permission to manage these cluster-scoped resources. Set `rbac.create=false`
only when equivalent permissions are provided externally.
`PORTAL_KUBERNETES_NAMESPACE` no longer limits discovery.

### Workload references

Optionally link a PortalSite to a workload in the same namespace:

```yaml
spec:
  id: home
  name: Home
  url: https://home.example.com
  workloadRef:
    kind: Deployment
    name: home
```

`kind` must be `Deployment`, `StatefulSet`, or `ReplicaSet` from `apps/v1`;
`name` is the workload's Kubernetes name. Namespace overrides are rejected.
The site appears only when the workload's desired `spec.replicas` is greater
than zero (omitted means one). This is a scaling check, not a readiness or
health check: a starting workload with desired replicas still appears.
Sites without `workloadRef` retain their existing behavior.

A zero replica count or missing workload hides the site from the web catalog
and client manifest; individual lookup returns no site and static-content
requests return 404. A missing workload also logs a warning. Other API errors
still fail the request. Availability is
checked on subsequent loads or requests, before reading ConfigMap content;
an already-open page does not refresh automatically. Hidden sites still
participate in duplicate-ID validation.

Chart RBAC additionally grants cluster-wide `get` on `apps` deployments,
statefulsets, and replicasets, with no workload list, watch, or write access.
With `rbac.create=false`, supply these permissions for referenced workloads
externally. Upgrade the CRD and RBAC before adding references to an existing
installation. Undeclared reference fields follow Kubernetes schema pruning.

## CRD lifecycle

The CRD is managed by this Helm release. Uninstalling the release removes the
CRD and every PortalSite resource in every namespace. The chart has no values
for `SITE_PATH`, probes, or disabling the Kubernetes provider, and it cannot
skip the CRD; see the [deployment guide](../docs/deployment.md).

## OCI packaging

GitHub Actions publishes the chart at `oci://ghcr.io/twsouthwick/charts/portal`
on pushes to `main` and stable `vMAJOR.MINOR.PATCH` tags. The chart version
and `appVersion` use MinVer. Main builds publish the MinVer prerelease image
tag plus the mutable `dev` tag; stable tags publish the exact version,
`MAJOR.MINOR`, `MAJOR`, and `latest` image tags. Main builds never move stable
image aliases. When `image.tag` is not overridden, the chart uses its
`appVersion`, so the chart and image remain aligned.

Authenticate Helm to GHCR to install a private chart:

```bash
helm install portal oci://ghcr.io/twsouthwick/charts/portal --version <version> -n default -f values.production.yaml
```

For a private image, configure registry pull credentials for the Kubernetes
service account or namespace before installing. Override `image.repository`
and `image.tag` when using a fork or a specific image version.

To package and publish a chart manually:

```bash
helm package ./chart
helm push portal-0.1.0.tgz oci://<registry>/charts
```
