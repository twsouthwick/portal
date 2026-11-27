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

```bash
helm package ./chart
helm push portal-0.1.0.tgz oci://<registry>/charts
```
