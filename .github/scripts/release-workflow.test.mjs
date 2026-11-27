import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const buildWorkflow = await readFile(new URL('../workflows/build.yaml', import.meta.url), 'utf8');
const chartValues = await readFile(new URL('../../chart/values.yaml', import.meta.url), 'utf8');
const deployment = await readFile(new URL('../../chart/templates/deployment.yaml', import.meta.url), 'utf8');

// @spec DELIVERY-013 DELIVERY-014 DELIVERY-015 DELIVERY-016
test('container release workflow uses MinVer channels and publishes both Linux architectures', () => {
  assert.equal((buildWorkflow.match(/ContainerRuntimeIdentifiers=linux-x64%3Blinux-arm64/g) ?? []).length, 2);
  assert.match(buildWorkflow, /containerTags/);
  assert.match(buildWorkflow, /MinVerVersion/);
  assert.match(buildWorkflow, /docker buildx imagetools inspect/);
  assert.match(buildWorkflow, /Platform: linux\/amd64/);
  assert.match(buildWorkflow, /Platform: linux\/arm64/);
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
