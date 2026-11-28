import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';

const buildWorkflow = await readFile(new URL('../workflows/build.yaml', import.meta.url), 'utf8');
const webProject = await readFile(new URL('../../Portal.Web/Portal.Web.csproj', import.meta.url), 'utf8');
const chartValues = await readFile(new URL('../../chart/values.yaml', import.meta.url), 'utf8');
const deployment = await readFile(new URL('../../chart/templates/deployment.yaml', import.meta.url), 'utf8');

// @spec DELIVERY-013 DELIVERY-014 DELIVERY-015 DELIVERY-016
test('container release workflow uses MinVer channels and publishes both Linux architectures', () => {
  assert.match(webProject, /<RuntimeIdentifiers>linux-x64;linux-arm64<\/RuntimeIdentifiers>/);
  assert.doesNotMatch(buildWorkflow, /ContainerRuntimeIdentifiers=/);
  assert.doesNotMatch(buildWorkflow, /dotnet publish[\s\S]{0,200}--no-restore/);
  assert.match(buildWorkflow, /containerTags/);
  assert.match(buildWorkflow, /MinVerVersion/);
  assert.match(buildWorkflow, /docker buildx imagetools inspect/);
  assert.match(buildWorkflow, /grep -Eq 'Platform:\[\[:space:\]\]\+linux\/amd64'/);
  assert.match(buildWorkflow, /grep -Eq 'Platform:\[\[:space:\]\]\+linux\/arm64'/);
  assert.match(buildWorkflow, /printf '%s\\n' "\$IMAGE_TAGS" \| tr ';' '\\n'/);
  assert.match(buildWorkflow, /helm package \.\/chart --version "\$VERSION" --app-version "\$VERSION"/);
  assert.doesNotMatch(buildWorkflow, /ContainerImageTags=.*latest/);
  assert.match(buildWorkflow, /node --test \.github\/scripts\/\*\.test\.mjs/);
  assert.match(buildWorkflow, /helm template portal \.\/chart/);
});

// @spec K8S-007
test('Helm defaults to the chart appVersion while preserving explicit image tag overrides', () => {
  assert.match(chartValues, /tag:\s*""/);
  assert.match(deployment, /\.Values\.image\.tag \| default \.Chart\.AppVersion/);
});

const service = await readFile(new URL('../../chart/templates/service.yaml', import.meta.url), 'utf8');

// @spec K8S-002
test('chart targets the port the image listens on', () => {
  assert.match(webProject, /<ContainerPort Include="8080" \/>/);
  assert.match(deployment, /containerPort: 8080/);
  assert.match(service, /targetPort: 8080/);
  assert.doesNotMatch(deployment + service, /8081\s*$/m);
});

// @spec K8S-008
test('chart trusts forwarded headers from the ingress by default', () => {
  assert.match(chartValues, /forwardedHeaders:\s*\n\s+enabled: true/);
  assert.match(deployment, /if \.Values\.forwardedHeaders\.enabled[\s\S]*?name: ASPNETCORE_FORWARDEDHEADERS_ENABLED\s+value: "true"/);
});

// @spec K8S-002 K8S-003 K8S-008
test('Helm renders cross-namespace reads and chart runtime settings', () => {
  const render = (namespace, overrides = []) => execFileSync('helm', [
    'template', 'portal', './chart', '--namespace', namespace,
    '--set', 'oidc.authority=https://identity.example.com',
    '--set', 'oidc.existingSecret=portal-oidc',
    ...overrides,
  ], { encoding: 'utf8' });
  const first = render('first');
  const second = render('second');
  assert.match(first, /kind: ClusterRole\nmetadata:\n  name: first-portal-portal-reader/);
  assert.match(second, /kind: ClusterRole\nmetadata:\n  name: second-portal-portal-reader/);
  assert.match(first, /kind: ClusterRoleBinding\nmetadata:\n  name: first-portal-portal-reader/);
  assert.match(first, /roleRef:\n  apiGroup: rbac.authorization.k8s.io\n  kind: ClusterRole\n  name: first-portal-portal-reader/);
  assert.match(first, /subjects:\n  - kind: ServiceAccount\n    name: portal-portal\n    namespace: first/);
  assert.match(first, /resources: \["portalsites"\]\n    verbs: \["list"\]/);
  assert.match(first, /resources: \["configmaps"\]\n    verbs: \["get"\]/);
  assert.match(first, /targetPort: 8080/);
  assert.match(first, /containerPort: 8080/);
  assert.match(first, /name: ASPNETCORE_FORWARDEDHEADERS_ENABLED\s+value: "true"/);
  assert.doesNotMatch(first, /kind: (Role|RoleBinding)\n|PORTAL_KUBERNETES_NAMESPACE/);
  assert.doesNotMatch(render('first', ['--set', 'rbac.create=false']), /kind: ClusterRole(?:Binding)?\n/);
  assert.doesNotMatch(render('first', ['--set', 'forwardedHeaders.enabled=false']), /ASPNETCORE_FORWARDEDHEADERS_ENABLED/);
});
