# Portal Helm chart

This chart installs the PortalSite CRD and optional example resources for the portal.

## Install

```bash
helm install portal ./chart -n default
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
