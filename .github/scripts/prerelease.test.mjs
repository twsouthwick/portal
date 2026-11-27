import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import {
  qualifies,
  selectRun,
  releaseDisposition,
  validateArtifact,
} from './prerelease.mjs';

const sha = 'a'.repeat(40);
const push = { event: 'push', head_branch: 'main', head_sha: sha, status: 'completed', conclusion: 'success', id: 12, run_attempt: 1 };

// @spec DELIVERY-008 DELIVERY-009
test('only main pushes qualify, never PRs, tags, or manual dispatch', () => {
  assert.equal(qualifies(push, sha), true);
  for (const change of [
    { event: 'pull_request' }, { event: 'workflow_dispatch' },
    { head_branch: 'v1.0.0' }, { head_sha: 'b'.repeat(40) },
  ]) assert.equal(qualifies({ ...push, ...change }, sha), false);
});

// @spec DELIVERY-008
test('the newest matching run must be completed and successful', () => {
  assert.equal(selectRun([push], sha).id, 12);
  assert.equal(selectRun([{ ...push, id: 13, status: 'in_progress' }, push], sha), null);
  assert.equal(selectRun([{ ...push, id: 13, conclusion: 'failure' }, push], sha), null);
  assert.equal(selectRun([{ ...push, event: 'pull_request', id: 14 }, push], sha).id, 12);
  assert.equal(selectRun([], sha), null);
});

// @spec DELIVERY-011
test('only matching drafts are retried; published releases are immutable', () => {
  const marker = '<!-- native-run:12-attempt:1 -->';
  assert.equal(releaseDisposition(null, marker), 'create');
  assert.equal(releaseDisposition({ draft: true, prerelease: true, body: marker }, marker), 'retry');
  assert.equal(releaseDisposition({ draft: false, prerelease: true, body: marker }, marker), 'complete');
  assert.throws(() => releaseDisposition({ draft: true, prerelease: true, body: '<!-- native-run:12-attempt:2 -->' }, marker));
  assert.throws(() => releaseDisposition({ draft: false, prerelease: false, body: marker }, marker));
});

// @spec DELIVERY-010 DELIVERY-012
test('artifact metadata must match the commit, run, attempt, architecture and checksum', () => {
  const expected = { sha, run: '12', attempt: '1', runner: 'Windows-arm64', name: 'portalclient.exe' };
  const contents = Buffer.from('binary');
  const checksum = createHash('sha256').update(contents).digest('hex');
  const provenance = `commit=${sha}\nrun=12\nattempt=1\nrunner=Windows-arm64\nrust=rustc\nnode=v24\n`;
  assert.equal(validateArtifact(expected, contents, `${checksum}  portalclient.exe`, provenance), checksum);
  assert.throws(() => validateArtifact(expected, Buffer.from('changed'), `${checksum}  portalclient.exe`, provenance));
  assert.throws(() => validateArtifact(expected, contents, `${checksum}  portalclient.exe`, provenance.replace('attempt=1', 'attempt=2')));
  assert.throws(() => validateArtifact(expected, contents, `${checksum}  portalclient.exe`, provenance.replace('Windows-arm64', 'Windows-x86_64')));
  assert.throws(() => validateArtifact({ ...expected, name: '../portalclient.exe' }, contents, `${checksum}  ../portalclient.exe`, provenance));
});
