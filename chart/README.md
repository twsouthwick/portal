# Portal Helm chart

This chart installs the PortalSite CRD and optional example resources for the portal.

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

Then install the chart:

```bash
helm install portal ./chart -n default -f values.production.yaml
```

To install the example resources as well:

```bash
--set exampleResources.enabled=true
```

## OCI packaging

GitHub Actions publishes the chart at `oci://ghcr.io/twsouthwick/charts/portal` on pushes to `main` and `v*` tags. It uses the same MinVer version as the image at `ghcr.io/twsouthwick/portal`. Authenticate Helm to GHCR to install a private chart:

```bash
helm install portal oci://ghcr.io/twsouthwick/charts/portal --version <version> -n default -f values.production.yaml
```

The chart defaults to the `latest` GHCR image. For a private image, configure registry pull credentials for the Kubernetes service account or namespace before installing. Override `image.repository` and `image.tag` when using a fork or a specific image version.

To package and publish a chart manually:

```bash
helm package ./chart
helm push portal-0.1.0.tgz oci://<registry>/charts
```
