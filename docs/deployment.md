# Deploying Portal

This guide is for operators running Portal on Kubernetes, especially when
starting with a file-backed catalog and later moving to `PortalSite`
resources. It describes the current behavior. Limitations are marked as
**Not supported**. The [Helm chart](../chart/README.md) is optional; every
step below can be done with plain manifests or Kustomize.

Match this guide to the source commit used to build your image and chart.
Earlier versions discover only one Kubernetes namespace and have different
chart port settings; select a published build containing the cross-namespace
and chart fixes before relying on the behavior described here.

## Versions and installation

### Published artifacts

| Artifact | Location | Published by |
|---|---|---|
| Container image (linux/amd64, linux/arm64) | `ghcr.io/twsouthwick/portal` | `build.yaml` on pushes to `main` and `v*` tags |
| Helm chart | `oci://ghcr.io/twsouthwick/charts/portal` | Same job; chart `version` and `appVersion` equal the image version |
| Native client builds | GitHub release `dev` | Not needed for the web deployment |

Versions come from [MinVer](https://github.com/adamralph/minver) with the tag
prefix `v`:

- A stable tag `vX.Y.Z` publishes the image tags `X.Y.Z`, `X.Y`, `X`, and
  `latest`, plus chart `X.Y.Z`.
- A push to `main` publishes image tag `<prerelease>` and the mutable `dev`
  tag, plus chart `<prerelease>`. Until the first stable tag exists, the
  prerelease is `0.0.0-alpha.0.<height>`, where `<height>` is
  `git rev-list --count <commit>` minus 1.

There is currently no stable tag, so `latest` does not exist. Use the newest
main build.

### Finding the newest build and its commit

The newest successful main build is the newest successful `build.yaml` push
run on `main`:

```bash
gh run list -R twsouthwick/portal --workflow build.yaml --branch main \
  --event push --status success -L 1 --json headSha,databaseId
```

The `dev` GitHub release is updated by the same push and names both values.
Its title is the version, and its body starts with
`Development build for commit <sha>`:

```bash
gh release view dev -R twsouthwick/portal --json name,body \
  --jq '.name, (.body | capture("commit (?<sha>[0-9a-f]{40})").sha)'
```

You can also compute the version for any commit: in a full clone, the version
is `0.0.0-alpha.0.$(( $(git rev-list --count <sha>) - 1 ))`. The run log for
the "Publish container" step prints `Pushed image index
'twsouthwick/portal:<version>, dev'`.

Images carry the `org.opencontainers.image.version` label but **no** revision
or source label. To confirm what an image is, compare its version label with
the release, or the exact tag's digest with the run log:

```bash
skopeo inspect --creds "<user>:<token>" docker://ghcr.io/twsouthwick/portal:dev \
  | jq '.Labels["org.opencontainers.image.version"], .Digest'
```

Pin deployments to the exact version tag (or the digest), not `dev`. Helm
needs the exact prerelease version (or `--devel`):

```bash
helm show chart oci://ghcr.io/twsouthwick/charts/portal --version '<exact-version>'
```

### Registry visibility and pull credentials

The source repository is public, but the GHCR image and chart packages are
**private**: anonymous pulls return `unauthorized`. To pull them:

1. Create a GitHub token with `read:packages` for an account that can read
   the packages.
2. Log in for Helm or skopeo: `helm registry login ghcr.io -u <user>` or
   `skopeo login ghcr.io`.
3. Give Kubernetes a pull secret and reference it from the pod's service
   account or pod spec:

   ```bash
   kubectl -n portal create secret docker-registry ghcr-pull \
     --docker-server=ghcr.io --docker-username=<user> --docker-password=<token>
   ```

The chart has no `imagePullSecrets` value. With the chart, patch its service
account instead, then restart the deployment so its new pods inherit the pull
secret:

```bash
RELEASE=portal
kubectl -n portal patch serviceaccount "${RELEASE}-portal" \
  -p '{"imagePullSecrets":[{"name":"ghcr-pull"}]}'
kubectl -n portal rollout restart deployment/"${RELEASE}-portal"
```

The package owner can also make the packages public in the GitHub package
settings, which removes the need for credentials.

### CRD and RBAC without Helm

No plain-manifest or Kustomize release assets are published. The CRD in the
chart is plain YAML with no templating, so you can apply it from a
commit-pinned raw URL. Use the commit that matches your image version (see
above):

```text
https://raw.githubusercontent.com/twsouthwick/portal/<sha>/chart/templates/crds/portalsites.portal.swick.app.yaml
```

The RBAC exists only as Helm templates. This is equivalent cluster-wide RBAC
for a Portal running in namespace `portal` as service account `portal`.
Creating the ClusterRole and ClusterRoleBinding requires cluster-scoped
permissions:

```yaml
# rbac.yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: portal
imagePullSecrets:
  - name: ghcr-pull
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: portal-reader
rules:
  - apiGroups: ["portal.swick.app"]
    resources: ["portalsites"]
    verbs: ["list"]
  - apiGroups: [""]
    resources: ["configmaps"]
    verbs: ["get"]
  - apiGroups: ["apps"]
    resources: ["deployments", "statefulsets", "replicasets"]
    verbs: ["get"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: portal-reader
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: portal-reader
subjects:
  - kind: ServiceAccount
    name: portal
    namespace: portal
```

For plain manifests, apply the pinned CRD and RBAC, then wait for the CRD to
be established:

```bash
kubectl apply -f 'https://raw.githubusercontent.com/twsouthwick/portal/<sha>/chart/templates/crds/portalsites.portal.swick.app.yaml'
kubectl wait --for=condition=Established --timeout=60s crd/portalsites.portal.swick.app
kubectl -n portal apply -f rbac.yaml
```

Replace `<sha>` with the matching commit and create the `portal` namespace
first if needed.

Kustomize can reference the pinned CRD URL directly:

```yaml
# kustomization.yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
namespace: portal
resources:
  - https://raw.githubusercontent.com/twsouthwick/portal/<sha>/chart/templates/crds/portalsites.portal.swick.app.yaml
  - rbac.yaml
  - deployment.yaml
```

To upgrade the CRD, change `<sha>` and apply it again. The CRD has one served
and stored version, `v1alpha1`. The `namespace:` setting does not affect it,
because CRDs are cluster-scoped.

The chart's ClusterRole grants `get` on **all** ConfigMaps cluster-wide. To
limit that access, remove its ConfigMap rule and instead apply this Role and
RoleBinding in each namespace containing referenced ConfigMaps, changing
`media` as needed:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: portal-configmap-reader
  namespace: media
rules:
  - apiGroups: [""]
    resources: ["configmaps"]
    verbs: ["get"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: portal-configmap-reader
  namespace: media
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: Role
  name: portal-configmap-reader
subjects:
  - kind: ServiceAccount
    name: portal
    namespace: portal
```

Keep the cluster-wide PortalSite `list` permission: namespace Roles alone
cannot authorize the cluster-wide list request. Portal still discovers
PortalSites in every namespace; if one references a ConfigMap without an
appropriate `get` grant, the catalog fails. Optionally restrict each Role to
specific ConfigMap names with `resourceNames`.

### CRD lifecycle and deletion

- The chart installs the CRD as a regular template in `templates/crds/`, not
  through Helm's `crds/` directory. Helm therefore creates, updates, and
  **deletes** it with the release. `helm uninstall` deletes the CRD, and
  Kubernetes then deletes **every `PortalSite` in every namespace**. The CRD
  has no `helm.sh/resource-policy: keep` annotation.
- Deleting the CRD by any other means, such as `kubectl delete`, or a GitOps
  prune after removing it from a Kustomization, has the same cluster-wide
  effect. Protect it from pruning, for example with
  `kustomize.toolkit.fluxcd.io/prune: disabled` (Flux) or
  `argocd.argoproj.io/sync-options: Prune=false,Delete=false` (Argo CD).
- The chart cannot skip its CRD. Install the CRD either through the chart or
  separately, not both. Helm refuses to adopt an existing CRD that it does not
  own.
- ConfigMaps referenced by `contentRef` are ordinary resources and are not
  deleted with the CRD.

## File-backed deployment in Kubernetes

### The Kubernetes provider turns itself on in pods

Portal reads `PortalSite` resources whenever **any** of these is true. There
is no configuration switch:

- `KUBERNETES_SERVICE_HOST` is non-empty. Kubelet sets it in every pod.
- `KUBECONFIG` is set.
- `~/.kube/config` exists.

In a pod it uses the mounted service-account token. To run file-only, override
the variable with an empty value. Container `env` entries take precedence over
the variables that kubelet injects. Also ensure `KUBECONFIG` is unset or empty
and the container has no default `~/.kube/config`; either would still enable
the provider:

```yaml
env:
  - name: KUBERNETES_SERVICE_HOST
    value: ""
```

You can also set `automountServiceAccountToken: false`, but only together with
the override. Otherwise the provider fails because the token is missing.

### Failure behavior when `SITE_PATH` is set

Neither provider is created until the first catalog or content request, so
startup succeeds even when the catalog sources are broken. Requests then
behave as follows:

| Condition | Startup | Site list (UI) and `/api/manifest` | `/<id>/content/...` |
|---|---|---|---|
| `SITE_PATH` missing or not a directory/`.zip` | OK | Fails on every request (UI error, manifest `503`) | Fails |
| Malformed `manifest.json`, such as an invalid `store` | OK | Fails on every request until restart | Fails |
| Kubernetes provider active, CRD missing (`404`) | OK | **Fails**, including file-backed sites | File sites work; unknown IDs return `500` |
| Kubernetes provider active, RBAC missing (`403`) | OK | **Fails**, including file-backed sites | File sites work; unknown IDs return `500` |
| Invalid or duplicate `PortalSite`, missing ConfigMap or key | OK | **Fails** | As above |

A Kubernetes error fails the whole catalog because every page load queries
every provider. For a file-only phase, **disable the Kubernetes provider** as
shown above. Before you enable it, install the CRD and RBAC.

Startup does fail without `Oidc:Authority`, `Oidc:ClientId`, and
`Oidc:ClientSecret`.

### Required settings

| Setting | Required | Notes |
|---|---|---|
| `Oidc__Authority` | Yes | Issuer URL, such as `https://auth.example.com` |
| `Oidc__ClientId` | Yes | OIDC client ID, such as `portal` |
| `Oidc__ClientSecret` | Yes | Plain-text client secret; load it from a Secret |
| `Oidc__RequireHttpsMetadata` | No | Default `true`; keep it `true` in production |
| `Oidc__ApiAudience` | No | Default `portal-api`; see [Audiences](#audiences) |
| `Oidc__NativeClientId` | No | Public client ID; without it native OIDC metadata is unavailable |
| `Oidc__NativeScopes` | No | Whitespace-separated native scopes; defaults to `openid groups` when unset or blank |
| `SITE_PATH` | For file catalogs | A directory, or a `.zip` file whose root contains `manifest.json` |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | Behind a TLS proxy | `true`; see [Reverse proxy](#reverse-proxy-and-tls) |
| `KUBERNETES_SERVICE_HOST` | File-only phase | `""` disables in-cluster detection when no kubeconfig exists |
| `PORTAL_KUBERNETES_NAMESPACE` | No | Legacy setting is ignored; discovery is cluster-wide |

The image runs as UID 1654 from `/app` and listens on **port 8080**
(`ASPNETCORE_HTTP_PORTS`). The `SITE_PATH` files must be readable by that UID.
No write access is needed except under `/tmp`. Data-protection keys, which
sign the login cookie, are stored in `/tmp/swick.portal.web/keys`, so:

- With `readOnlyRootFilesystem: true`, mount an `emptyDir` at `/tmp`.
- The keys are ephemeral. After a restart users must sign in again, and
  running more than one replica breaks sign-in. Run **one replica**.

Mount options for `SITE_PATH`:

- **ZIP in a ConfigMap or Secret**: create an archive of the site directory
  with `manifest.json` at its root. Store it as `binaryData` (limit 1 MiB) and
  set `SITE_PATH=/sites/sites.zip`.
- **Directory from a ConfigMap**: ConfigMap keys cannot contain `/`, so map
  each key to a nested path with `items`, as in the example below.
- **Directory on a volume** (PVC, NFS, or hostPath): set `SITE_PATH` to the
  mounted directory.

### When file changes take effect

- `manifest.json` files and `usage.md` are read **once**, on the first catalog
  request after startup. To apply changes, **restart the pod** (for example
  with `kubectl rollout restart`).
- A ZIP is opened once. Restart after you replace it.
- Files under a directory source's `content/` folder are read from disk on
  each request, so additions and edits are served without a restart.
- Kubelet updates mounted ConfigMaps after a delay, but not when they are
  mounted with `subPath`. Portal still needs a restart to re-read manifests.

### Example file-only deployment

Replace `<exact-version>` with a published version matching the intended
source commit.

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: portal-sites
data:
  root.json: |
    { "name": "Homelab", "description": "Applications on the home network" }
  jellyfin.json: |
    { "id": "jellyfin", "name": "Jellyfin", "url": "https://jellyfin.example.com",
      "icon": "bi bi-film", "roles": ["media"] }
  jellyfin.md: |
    Sign in with your homelab account.
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: portal
spec:
  replicas: 1
  selector:
    matchLabels: { app: portal }
  template:
    metadata:
      labels: { app: portal }
    spec:
      serviceAccountName: portal
      automountServiceAccountToken: false
      containers:
        - name: portal
          image: ghcr.io/twsouthwick/portal:<exact-version>
          ports:
            - name: http
              containerPort: 8080
          env:
            - name: KUBERNETES_SERVICE_HOST
              value: ""
            - name: SITE_PATH
              value: /sites
            - name: ASPNETCORE_FORWARDEDHEADERS_ENABLED
              value: "true"
            - name: Oidc__Authority
              value: https://auth.example.com
            - name: Oidc__ClientId
              value: portal
            - name: Oidc__ClientSecret
              valueFrom:
                secretKeyRef: { name: portal-oidc, key: client-secret }
          readinessProbe:
            httpGet: { path: /favicon.png, port: http }
          livenessProbe:
            tcpSocket: { port: http }
          securityContext:
            readOnlyRootFilesystem: true
            allowPrivilegeEscalation: false
          volumeMounts:
            - { name: sites, mountPath: /sites, readOnly: true }
            - { name: tmp, mountPath: /tmp }
      volumes:
        - name: sites
          configMap:
            name: portal-sites
            items:
              - { key: root.json, path: manifest.json }
              - { key: jellyfin.json, path: jellyfin/manifest.json }
              - { key: jellyfin.md, path: jellyfin/usage.md }
        - name: tmp
          emptyDir: {}
---
apiVersion: v1
kind: Service
metadata:
  name: portal
spec:
  selector: { app: portal }
  ports:
    - name: http
      port: 80
      targetPort: http
```

The Helm chart currently has no values for `SITE_PATH` volumes, extra
environment variables, probes, or disabling the Kubernetes provider. For a
file-backed deployment, use plain manifests like the ones above.

## Authentication and networking

### Authelia OIDC client

Portal uses the authorization code flow with PKCE, a form-post response, and
`client_secret_post` client authentication. It requests the scopes `openid`,
`profile`, and `groups`. It reads the display name from the `name` claim and
roles from the `groups` claim of the **ID token**. Portal does not call the
userinfo endpoint, so those claims must be in the ID token. Authelia 4.39 or
later supports this through a claims policy. By default, the `groups` scope
makes the groups claim available from UserInfo; the claims policy's `id_token`
list also places it in the ID token for Portal's web sign-in. Its
`access_token` list places groups in JWT access tokens for the native client
(see [Native Authelia client](#native-authelia-client)); it requires Authelia
4.39.16 or later and has no effect on the web client's opaque access tokens.

```yaml
identity_providers:
  oidc:
    claims_policies:
      portal:
        id_token: ['groups', 'name']
        access_token: ['groups']
    clients:
      - client_id: 'portal'
        client_name: 'Portal'
        # authelia crypto hash generate pbkdf2 --variant sha512 --random --random.length 72 --random.charset rfc3986
        client_secret: '$pbkdf2-sha512$...'
        public: false
        claims_policy: 'portal'
        authorization_policy: 'one_factor'
        consent_mode: 'implicit'    # optional: skip the consent screen
        require_pkce: true
        pkce_challenge_method: 'S256'
        redirect_uris:
          - 'https://portal.example.com/signin-oidc'
        scopes: ['openid', 'profile', 'groups']
        response_types: ['code']
        response_modes: ['form_post']
        grant_types: ['authorization_code']
        token_endpoint_auth_method: 'client_secret_post'
```

- **Callback URI:** `https://<portal-host>/signin-oidc`, which must match
  exactly. Portal builds it from the request's scheme and host, so a
  TLS-terminating proxy must forward `X-Forwarded-Proto` and Portal must
  trust it (see below). Otherwise Authelia rejects an `http://` redirect URI.
- **Client secret:** Authelia stores the hash. Put the plain-text value in a
  Kubernetes Secret used as `Oidc__ClientSecret`:
  `kubectl -n portal create secret generic portal-oidc --from-literal=client-secret='<plain secret>'`.
- **Groups:** each Authelia group name becomes a Portal role. A site's `roles`
  are matched against those names. A site with no roles is visible to any
  authenticated user. Grant the `groups` scope. Authelia returns the groups
  from UserInfo by default; the claims policy above is needed because Portal's
  web sign-in reads the ID token instead.
- Session cookie: `portal.session`, which is `Secure`, `HttpOnly`, and
  `SameSite=Lax`.

For a local reference, see `Portal.AppHost/Authelia/configuration.yml`.

### Audiences

- **Web sign-in:** the ID token audience is the client ID. Authelia sets this
  automatically, and no extra audience configuration is needed.
- **Client API (`/api/manifest`):** accepts either the web session cookie or
  a signed JWT bearer access token whose audience is `Oidc__ApiAudience`
  (default `portal-api`) and whose `groups` claim supplies the user's roles.
  An ID token is not an API access token and is rejected. **No audience setup
  is needed for the web deployment.**

### Native Authelia client

A native app needs a separate public client; it must not use the confidential
`portal` client's secret. For the Linux loopback flow, a client can be
registered like this:

```yaml
identity_providers:
  oidc:
    clients:
      - client_id: 'portal-native'
        client_name: 'Portal Native'
        client_secret: ''
        public: true
        claims_policy: 'portal'
        authorization_policy: 'one_factor'
        redirect_uris:
          - 'http://127.0.0.1:49152/callback'
        scopes: ['openid', 'groups', 'offline_access']
        response_types: ['code']
        grant_types: ['authorization_code', 'refresh_token']
        token_endpoint_auth_method: 'none'
        require_pkce: true
        pkce_challenge_method: 'S256'
        audience: ['portal-api']
        requested_audience_mode: 'explicit'
        access_token_signed_response_alg: 'RS256'
```

The `access_token: ['groups']` entry in the `portal` claims policy places the
user's groups in the RS256 access JWT, which `/api/manifest` requires; Portal
does not call UserInfo. Authelia 4.39.15 accepts that setting but ignores it,
so use 4.39.16 or later. Authelia 4.39.28 was verified to accept a different
port on the registered `127.0.0.1` callback, issue a refresh token, and issue
an access JWT with the `portal-api` audience and `groups`, which
`/api/manifest` accepted with role filtering applied. Do not send the ID token
as the API bearer token.

### Reverse proxy and TLS

Portal serves plain HTTP on port 8080 and expects TLS to be terminated by the
ingress (for example, Traefik). Set
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. The Helm chart sets it by default
(`forwardedHeaders.enabled`). With it, Portal:

- Uses `X-Forwarded-Proto` to build the `https://` OIDC callback.
- Uses `X-Forwarded-For` as the client address. `isInternal` depends on this.
  Without it, every client appears to come from the proxy's private address.

This setting trusts forwarded headers from any source. Make Portal reachable
only through the ingress, for example with a ClusterIP Service and, if needed,
a NetworkPolicy.

### Traefik forward-auth

Portal enforces OIDC itself and ignores forward-auth headers such as
`Remote-User` and `Remote-Groups`. A forward-auth middleware in front of Portal
is redundant. The recommended setup is to **leave it off Portal's route**.

If you keep it:

- Sign-in still works when Authelia and Portal share a parent domain, such as
  `auth.example.com` and `portal.example.com`. Authelia's SSO session
  completes the OIDC redirect without a prompt, and the browser sends the
  Authelia cookie with the `POST /signin-oidc` callback, so forward-auth lets
  it through. If they are on different sites, forward-auth redirects the
  callback POST and sign-in fails.
- `/api/manifest` bearer requests from a future native client must bypass
  forward-auth.
- **Sign-out:** Portal's sign-out (`POST /auth/logout`) clears only Portal's
  cookie and shows `/auth/signed-out`. It does not end the Authelia session,
  and forward-auth stays satisfied. The next visit signs in again without a
  prompt. To sign out completely, also visit Authelia's logout URL
  (`https://auth.example.com/logout`).

### Outbound connections

| Destination | When | Notes |
|---|---|---|
| OIDC authority (`/.well-known/openid-configuration`, JWKS, token endpoint) | First sign-in and periodically after | The pod itself must resolve the authority URL and trust its TLS certificate. If Authelia is in the same cluster, the public hostname must resolve from inside it. For a private CA, mount the CA bundle and set `SSL_CERT_FILE` or `SSL_CERT_DIR`. |
| Kubernetes API (`kubernetes.default.svc`) | Provider enabled | `list` PortalSites; `get` referenced workloads and available sites' ConfigMaps on catalog loads |
| OTLP endpoint | `OTEL_EXPORTER_OTLP_ENDPOINT` set | Optional telemetry |

If the authority cannot be reached, unauthenticated requests return `500`
instead of redirecting to sign-in.

### Health and readiness

`/health` and `/alive` are mapped **only in the Development environment**. In
production, every route requires sign-in except static assets and
`/auth/signed-out`. `/` returns `302` (redirect to sign-in) when the IdP is
reachable and `500` when it is not. For rollout checks:

- **Readiness:** `httpGet /favicon.png` on port 8080 returns `200` without
  authentication once the app is serving.
- **Liveness:** `tcpSocket` on port 8080.
- **End-to-end check:** `curl -sI https://portal.example.com/` should return
  `302` with a `Location` on the Authelia host whose `redirect_uri` is
  `https://portal.example.com/signin-oidc`. Then sign in through a browser.

## Cross-namespace PortalSites

Portal discovers `PortalSite` resources across **all** namespaces. It uses
the cluster-wide list API; there is no watch or namespace filter to configure.
`PORTAL_KUBERNETES_NAMESPACE` no longer limits discovery. Resources can be
placed alongside their applications.

Before upgrading from a single-namespace version, inspect PortalSites across
the entire cluster, not just the previously configured namespace. Additional
sites become visible, and duplicates or errors in any namespace can break the
catalog. The service account also needs the cluster-wide permissions below.

- **Minimum RBAC:** cluster-wide `list` on `portalsites.portal.swick.app`,
  plus `get` on referenced `configmaps` and linked `apps` deployments,
  statefulsets, or replicasets in each resource's namespace. The
  chart grants these permissions cluster-wide; see
  [CRD and RBAC](#crd-and-rbac-without-helm) for a narrower ConfigMap option.
  Portal does not use `watch` or `get` on PortalSites.
- **`contentRef`** resolves in the `PortalSite`'s own namespace, not Portal's
  namespace. Missing resource namespace metadata is an error.
- **`workloadRef`** optionally links an `apps/v1` Deployment, StatefulSet, or
  ReplicaSet by `kind` and `name` in the PortalSite's namespace. Zero desired
  replicas or a missing workload hides the site from catalog and client
  manifest, and makes content lookup return 404. Readiness is not checked.
  Missing workloads log warnings; other API failures remain errors. Update
  CRD and RBAC before using this feature; see the
  [workload example](../chart/README.md#workload-references).
- **Duplicate IDs:** two `PortalSite` resources with the same `spec.id`,
  case-insensitive, fail the whole catalog, including when they are in
  different namespaces, one is hidden by workload status, or a file entry
  overrides that ID.
- **Errors:** API, permission, configuration, and validation errors fail the
  catalog request rather than being skipped (see
  [Failure behavior](#failure-behavior-when-site_path-is-set)).
- **Change visibility:** Portal has no watch or cache. Catalog loads list
  all resources, read linked workloads, and fetch available sites' referenced
  ConfigMaps; a content request for a
  Kubernetes-backed site lists all resources and reads the requested site's
  workload status before fetching its referenced ConfigMap. Changes appear on
  the next load or request, without a
  restart. An already-open page does not refresh by itself.

## Migration and behavior parity

### Equivalent definitions

File-backed site (`sites/jellyfin/manifest.json`, plus `usage.md` and
`content/logo.png`):

```json
{
  "id": "jellyfin",
  "name": "Jellyfin",
  "description": "Movies and TV",
  "url": "https://jellyfin.example.com",
  "icon": "bi bi-film",
  "categories": ["media"],
  "roles": ["media"],
  "isPrivate": false,
  "isInternal": true,
  "apps": [
    { "name": "Jellyfin", "scheme": "jellyfin://", "store": "google",
      "storeId": "https://play.google.com/store/apps/details?id=org.jellyfin.mobile" },
    { "name": "Jellyfin", "scheme": "jellyfin://", "store": "apple",
      "storeId": "https://apps.apple.com/app/id1480192618" }
  ]
}
```

Equivalent `PortalSite`:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: jellyfin-portal-content
  namespace: media
binaryData:
  logo.png: <base64>
---
apiVersion: portal.swick.app/v1alpha1
kind: PortalSite
metadata:
  name: jellyfin
  namespace: media
spec:
  id: jellyfin
  name: Jellyfin
  description: Movies and TV
  url: https://jellyfin.example.com
  icon: bi bi-film
  categories: [media]
  roles: [media]
  isPrivate: false
  isInternal: true
  usageInstructions: |
    Sign in with your homelab account.
  apps:
    - name: Jellyfin
      scheme: jellyfin://
      store: google
      storeId: https://play.google.com/store/apps/details?id=org.jellyfin.mobile
    - name: Jellyfin
      scheme: jellyfin://
      store: apple
      storeId: https://apps.apple.com/app/id1480192618
  contentRef:
    name: jellyfin-portal-content
```

Field mapping and differences:

- `usage.md` becomes `spec.usageInstructions`. In file manifests, a
  `usageInstructions` JSON property is always replaced by `usage.md` (or by
  an empty string when that file is absent).
- `content/<path>` becomes a ConfigMap key served at `/<id>/content/<key>`.
  ConfigMap keys are flat filenames, so nested paths such as
  `content/guides/setup.pdf` cannot be reproduced. Flatten them, for example
  to `setup.pdf`, and update links. ConfigMaps are limited to 1 MiB in total.
  `contentRef.key` exposes only one key.
- **App links:** `store` is a **single string**, not an array, and `storeId`
  is required. Valid values are `none`, `apple`, `macos`, `google`,
  `microsoft`, `windows`, and `linux`, case-insensitive. Use one app entry per
  store. In files, an unknown value or an array fails the whole catalog. In a
  `PortalSite`, an unknown value is logged and treated as `none`. Only
  `apple`, `google`, and `microsoft`/`windows` are shown, on iOS/macOS,
  Android, and Windows respectively.
- `id` in a `PortalSite` must match `^[a-z0-9]([-a-z0-9]*[a-z0-9])?$` (at most
  63 characters) and `name` must be non-empty. File IDs are not validated, so
  rename any file ID that does not fit before migrating.
- `roles`: an empty list or no list means any authenticated user.
  `isPrivate`: shown only in private browsing. `isInternal`: disabled for
  clients with a public IP (requires forwarded headers).

### Precedence and root metadata

- When the file source and Kubernetes define the same `id`, compared
  case-insensitively, the **file entry wins** silently. The `PortalSite` is
  ignored for both the list and the content. Duplicate IDs within Kubernetes,
  even across namespaces, are an error (see above).
- The portal **name and description** come from the root `manifest.json` of
  the file source. With only Kubernetes, the name is `Portal` and the
  description is empty; `PortalSite` cannot set them. To keep them, leave
  `SITE_PATH` pointing to a directory that contains only the root
  `manifest.json` after all sites have moved.

### Incremental cutover

1. **File-only baseline.** Deploy with `SITE_PATH`,
   `KUBERNETES_SERVICE_HOST=""`, and the settings above. Verify sign-in, the
   site list for each role, app buttons, usage text, and every content URL.
2. **Enable Kubernetes.** Apply the CRD and cluster-wide RBAC. Before enabling
   the provider, inspect existing PortalSites in all namespaces for duplicates
   and invalid references. Then remove the `KUBERNETES_SERVICE_HOST` override
   and `automountServiceAccountToken: false` and roll out with the
   service-account token mounted. Previously existing PortalSites in any
   namespace will now appear unless a file entry overrides them. Check for
   catalog errors. **Rollback:** restore the override and roll out.
3. **Shadow each site.** For one site, apply its ConfigMap and a `PortalSite`
   in the application's namespace with the **same `id`**. Check it with
   `kubectl apply --dry-run=server -f site.yaml` first. Because the file entry
   wins, users see no change. However, Portal now lists and validates the
   resource on every load, so reload the portal and confirm the catalog still
   renders. A duplicate ID, schema problem, or missing ConfigMap or key breaks
   the catalog immediately. Before removing the file entry, verify the
   Kubernetes version using a separate, access-restricted staging Portal
   instance with `SITE_PATH` unset and Kubernetes access enabled. Register its
   OIDC callback and check visibility for each role, app links, usage text, and
   each content URL. Its root portal metadata will use the Kubernetes defaults.
   **Rollback:** delete the `PortalSite`.
4. **Remove the file entry.** Delete that site's directory (or ConfigMap
   `items`) and restart Portal. Verify the site's visibility for each role,
   its apps, its usage text, and each `/<id>/content/<key>` URL.
   **Rollback:** restore the file entry and restart. The file entry takes
   precedence again.
5. Repeat steps 3 and 4 for each site. At the end, keep only the root
   `manifest.json` under `SITE_PATH` to preserve the portal name and
   description.

Avoid `helm uninstall` and CRD deletion during and after migration. Either
one deletes every `PortalSite` (see [CRD lifecycle](#crd-lifecycle-and-deletion)).
