import { test } from 'node:test';
import assert from 'node:assert/strict';
import { containerTags, releaseChannel } from './release-policy.mjs';

// @spec DELIVERY-009 DELIVERY-013 DELIVERY-014
test('only main pushes and stable version tags select a release channel', () => {
  assert.equal(releaseChannel({ event: 'push', head_branch: 'main' }), 'dev');
  assert.equal(releaseChannel({ event: 'push', head_branch: 'v0.1.0' }), 'stable');
  assert.equal(releaseChannel({ event: 'push', head_branch: 'v12.34.56' }), 'stable');
  assert.equal(releaseChannel({ event: 'push', head_branch: 'v0.1.0-rc.1' }), null);
  assert.equal(releaseChannel({ event: 'push', head_branch: 'feature' }), null);
  assert.equal(releaseChannel({ event: 'pull_request', head_branch: 'main' }), null);
  assert.equal(releaseChannel({ event: 'workflow_dispatch', head_branch: 'main' }), null);
});

// @spec DELIVERY-013 DELIVERY-014
test('container tags separate rolling development from stable aliases', () => {
  assert.deepEqual(containerTags('0.1.1-alpha.0.4', 'dev'), ['0.1.1-alpha.0.4', 'dev']);
  assert.deepEqual(containerTags('0.1.0', 'stable'), ['0.1.0', '0.1', '0', 'latest']);
  assert.throws(() => containerTags('0.1.0-rc.1', 'stable'), /stable version/);
  assert.throws(() => containerTags('0.1.0', 'dev'), /prerelease version/);
});
