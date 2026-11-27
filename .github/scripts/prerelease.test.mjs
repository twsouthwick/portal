import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import {
  qualifies,
  selectRun,
  releaseDisposition,
  findRelease,
  validateArtifact,
  publish,
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

// @spec DELIVERY-011
test('drafts are found in the release listing and duplicate tags are rejected', () => {
  const draft = { draft: true, tag_name: `dev-${sha}` };
  assert.equal(findRelease([draft], draft.tag_name), draft);
  assert.equal(findRelease([], draft.tag_name), null);
  assert.throws(() => findRelease([draft, draft], draft.tag_name));
});

// @spec DELIVERY-008 DELIVERY-009 DELIVERY-011
test('publisher workflow serializes by commit and denies non-main builds the write job', async () => {
  const workflow = await readFile(new URL('../workflows/prerelease.yaml', import.meta.url), 'utf8');
  assert.match(workflow, /group: native-prerelease-\$\{\{ github\.event\.workflow_run\.head_sha \}\}/);
  assert.match(workflow, /cancel-in-progress: false/);
  assert.match(workflow, /github\.event\.workflow_run\.event == 'push'/);
  assert.match(workflow, /github\.event\.workflow_run\.head_branch == 'main'/);
  assert.match(workflow, /permissions: \{\}/);
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

// @spec DELIVERY-008 DELIVERY-009
test('failed or untrusted source runs do not mutate a release', async () => {
  const options = { event: { workflow_run: { ...push, head_repository: { id: 123 } } }, repository: 'o/r', repositoryId: '123',
    apiUrl: 'https://api.github.com', token: 'test', publisherRunId: '1' };
  let requests = 0;
  const request = async (_url, init) => {
    requests++;
    assert.equal(init.method, undefined);
    return new Response(JSON.stringify({ workflow_runs: [{ ...push, conclusion: 'failure' }] }));
  };
  await publish({ ...options, request });
  assert.equal(requests, 1);
  await assert.rejects(publish({ ...options, event: { workflow_run: { ...push, event: 'pull_request' } }, request }), /Untrusted trigger/);
  assert.equal(requests, 1);
});

// @spec DELIVERY-008 DELIVERY-010 DELIVERY-011 DELIVERY-012
test('publication stays draft after interruption and retries the same attempt without mixing assets', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'portal-prerelease-test-'));
  let ref = null;
  let release = null;
  let interrupt = true;
  const mutations = [];
  const result = (data, status = 200) => new Response(data === null ? null : JSON.stringify(data), { status });
  const request = async (url, options = {}) => {
    const path = new URL(url).pathname.replace('/repos/o/r', '');
    const method = options.method ?? 'GET';
    const body = typeof options.body === 'string' ? JSON.parse(options.body) : null;
    if (path.includes('/actions/workflows/')) return result({ workflow_runs: [push] });
    if (path.startsWith('/git/ref/tags/')) return ref ? result(ref) : result(null, 404);
    if (path === '/releases' && method === 'GET') return result(release ? [release] : []);
    if (method !== 'GET') mutations.push(`${method} ${path}`);
    if (path === '/git/refs' && method === 'POST') return result(ref = { object: { type: 'commit', sha: body.sha } });
    if (path === '/releases' && method === 'POST') {
      release = { ...body, id: 1, assets: [], upload_url: 'https://uploads.github.com/repos/o/r/releases/1/assets{?name,label}' };
      return result(release);
    }
    if (path === '/releases/1/assets' && method === 'POST') {
      const name = new URL(url).searchParams.get('name');
      if (interrupt && release.assets.length === 2) throw new Error('simulated upload interruption');
      const asset = { id: release.assets.length + 1, name, state: 'uploaded' };
      release.assets.push(asset);
      return result(asset);
    }
    if (path.startsWith('/releases/assets/') && method === 'DELETE') {
      release.assets = release.assets.filter(asset => asset.id !== Number(path.split('/').pop()));
      return result(null, 204);
    }
    if (path === '/releases/1' && method === 'GET') return result(release);
    if (path === '/releases/1' && method === 'PATCH') {
      Object.assign(release, body);
      return result(release);
    }
    throw new Error(`Unexpected request: ${method} ${url}`);
  };
  const download = async (_command, args) => {
    assert.equal(args[2], '12');
    const artifact = args[args.indexOf('--name') + 1];
    const directory = args[args.indexOf('--dir') + 1];
    const windows = artifact.includes('-windows-');
    const arch = artifact.includes('-arm64-') ? 'arm64' : 'x86_64';
    const name = windows ? 'portalclient.exe' : 'Portal.AppImage';
    const contents = Buffer.from(artifact);
    const hash = createHash('sha256').update(contents).digest('hex');
    await writeFile(join(directory, name), contents);
    await writeFile(join(directory, 'SHA256SUMS'), `${hash}  ${name}\n`);
    await writeFile(join(directory, 'provenance.txt'), `commit=${sha}\nrun=12\nattempt=1\nrunner=${windows ? 'Windows' : 'Linux'}-${arch}\nrust=rustc\nnode=v24\n`);
  };
  const options = { event: { workflow_run: { ...push, head_repository: { id: 123 } } }, repository: 'o/r', repositoryId: '123',
    apiUrl: 'https://api.github.com', token: 'test', publisherRunId: '1', request, download, tempRoot: dir };
  try {
    await assert.rejects(publish(options), /simulated upload interruption/);
    assert.equal(release.draft, true);
    assert.equal(release.assets.length, 2);
    interrupt = false;
    await publish({ ...options, publisherRunId: '2' });
    assert.equal(release.draft, false);
    assert.equal(release.prerelease, true);
    assert.deepEqual(release.assets.map(a => a.name).sort(), [
      'SHA256SUMS', 'portal-client-linux-arm64.AppImage', 'portal-client-linux-x86_64.AppImage',
      'portal-client-windows-arm64.exe', 'portal-client-windows-x86_64.exe', 'provenance.json',
    ].sort());
    const before = mutations.length;
    await publish({ ...options, publisherRunId: '3' });
    assert.equal(mutations.length, before);
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});
