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
const push = { event: 'push', head_branch: 'main', head_sha: sha, status: 'completed', conclusion: 'success', id: 12, run_number: 12, run_attempt: 1 };

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
  assert.equal(selectRun([{ ...push, head_branch: 'v0.1.0' }], sha, 'v0.1.0').id, 12);
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
  assert.equal(releaseDisposition({ draft: false, prerelease: true, tag_name: 'dev', body: '<!-- previous run -->' }, marker, 'dev'), 'update');
  assert.equal(releaseDisposition({ draft: false, prerelease: false, tag_name: 'v1.0.0', body: marker }, marker), 'complete');
  assert.throws(() => releaseDisposition({ draft: true, prerelease: true, body: '<!-- native-run:12-attempt:2 -->' }, marker));
  assert.throws(() => releaseDisposition({ draft: false, prerelease: true, body: marker }, marker, 'stable'));
});

// @spec DELIVERY-008 DELIVERY-011 DELIVERY-017
test('the rolling dev release is replaceable while stable releases stay immutable', () => {
  const marker = '<!-- native-run:12-attempt:1 -->';
  assert.equal(releaseDisposition(null, marker, 'dev'), 'create');
  assert.equal(releaseDisposition({ draft: false, prerelease: true, tag_name: 'dev' }, marker, 'dev'), 'update');
  assert.equal(releaseDisposition({ draft: true, prerelease: true, tag_name: 'dev', body: marker }, marker, 'dev'), 'retry');
  assert.equal(releaseDisposition({ draft: false, prerelease: false, tag_name: 'v0.1.0' }, marker, 'stable'), 'complete');
  assert.throws(() => releaseDisposition(
    { draft: false, prerelease: false, tag_name: 'v0.1.0' },
    marker,
    'dev',
  ));
});

// @spec DELIVERY-011
test('drafts are found in the release listing and duplicate tags are rejected', () => {
  const draft = { draft: true, tag_name: `dev-${sha}` };
  assert.equal(findRelease([draft], draft.tag_name), draft);
  assert.equal(findRelease([], draft.tag_name), null);
  assert.throws(() => findRelease([draft, draft], draft.tag_name));
});

// @spec DELIVERY-008 DELIVERY-009 DELIVERY-011
test('publisher workflow serializes releases and admits only main or stable-tag pushes', async () => {
  const workflow = await readFile(new URL('../workflows/prerelease.yaml', import.meta.url), 'utf8');
  assert.match(workflow, /group: native-release-\$\{\{ github\.repository \}\}/);
  assert.match(workflow, /cancel-in-progress: false/);
  assert.match(workflow, /github\.event\.workflow_run\.event == 'push'/);
  assert.match(workflow, /github\.event\.workflow_run\.head_branch == 'main'/);
  assert.match(workflow, /startsWith\(github\.event\.workflow_run\.head_branch, 'v'\)/);
  assert.match(workflow, /permissions: \{\}/);
});

// @spec DELIVERY-010 DELIVERY-012
test('artifact metadata must match the commit, run, attempt, architecture and checksum', () => {
  const expected = { sha, run: '12', attempt: '1', runner: 'Windows-arm64', name: 'portalclient.exe' };
  const contents = Buffer.from('binary');
  const checksum = createHash('sha256').update(contents).digest('hex');
  const provenance = `commit=${sha}\nrun=12\nattempt=1\nrunner=Windows-arm64\nversion=0.1.1-alpha.0.4\nrust=rustc\nnode=v24\n`;
  assert.equal(validateArtifact({ ...expected, version: '0.1.1-alpha.0.4' }, contents, `${checksum}  portalclient.exe`, provenance), checksum);
  assert.throws(() => validateArtifact(expected, Buffer.from('changed'), `${checksum}  portalclient.exe`, provenance));
  assert.throws(() => validateArtifact(expected, contents, `${checksum}  portalclient.exe`, provenance.replace('attempt=1', 'attempt=2')));
  assert.throws(() => validateArtifact(expected, contents, `${checksum}  portalclient.exe`, provenance.replace('Windows-arm64', 'Windows-x86_64')));
  assert.throws(() => validateArtifact({ ...expected, version: '0.1.0' }, contents, `${checksum}  portalclient.exe`, provenance));
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

// @spec DELIVERY-017 DELIVERY-018
test('a stable release is immutable and an older main run cannot replace dev', async () => {
  const stableSha = 'b'.repeat(40);
  const calls = [];
  const result = (data, status = 200) => new Response(data === null ? null : JSON.stringify(data), { status });
  const stableRelease = { id: 2, tag_name: 'v0.1.0', name: 'Portal v0.1.0', draft: false, prerelease: false, body: 'stable', assets: [] };
  const devRelease = {
    id: 3, tag_name: 'dev', draft: false, prerelease: true,
    body: '<!-- native-run:99-attempt:1-run-number:13 -->', assets: [],
  };
  let release = stableRelease;
  let workflowBranch = 'v0.1.0';
  let workflowSha = stableSha;
  const request = async (url, options = {}) => {
    const path = new URL(url).pathname.replace('/repos/o/r', '');
    const method = options.method ?? 'GET';
    calls.push(`${method} ${path}`);
    if (path.includes('/actions/workflows/')) {
      return result({ workflow_runs: [{
        ...push, head_branch: workflowBranch, head_sha: workflowSha,
        html_url: 'https://github.com/o/r/actions/runs/12',
      }] });
    }
    if (path === '/git/ref/tags/v0.1.0') return result({ object: { type: 'tag', sha: 'd'.repeat(40) } });
    if (path === `/git/tags/${'d'.repeat(40)}`) return result({ object: { type: 'commit', sha: stableSha } });
    if (path === '/git/ref/tags/dev') return result({ object: { type: 'commit', sha: 'c'.repeat(40) } });
    if (path === '/releases' && method === 'GET') return result([release]);
    throw new Error(`Unexpected request: ${method} ${url}`);
  };
  const base = {
    repository: 'o/r', repositoryId: '123', apiUrl: 'https://api.github.com', token: 'test',
    publisherRunId: '1', request, download: () => { throw new Error('Already-published releases must not download artifacts'); },
  };
  const stableEvent = { workflow_run: { ...push, head_branch: 'v0.1.0', head_sha: stableSha, head_repository: { id: 123 } } };
  await publish({ ...base, event: stableEvent });
  assert.deepEqual(calls.filter(call => !call.startsWith('GET ')), []);

  calls.length = 0;
  workflowBranch = 'main';
  workflowSha = sha;
  release = devRelease;
  const oldMainEvent = { workflow_run: { ...push, head_repository: { id: 123 } } };
  await publish({ ...base, event: oldMainEvent });
  assert.deepEqual(calls.filter(call => !call.startsWith('GET ')), []);
});

async function publicationFixture({ channel = 'dev', rerunOnRecheck = false } = {}) {
  const dir = await mkdtemp(join(tmpdir(), 'portal-prerelease-test-'));
  const tag = channel === 'dev' ? 'dev' : 'v0.1.0';
  const branch = channel === 'dev' ? 'main' : tag;
  const version = channel === 'dev' ? '0.1.1-alpha.0.4' : '0.1.0';
  const state = {
    ref: { object: { type: 'commit', sha: channel === 'dev' ? 'c'.repeat(40) : sha } },
    release: null, nativeId: 12, nativeRunNumber: 12, nativeAttempt: 1, version, assetContents: new Map(),
  };
  const mutations = [];
  const workflowRequests = new Map();
  let nextAssetId = 1;
  const result = (data, status = 200) => new Response(data === null ? null : JSON.stringify(data), { status });
  const request = async (url, options = {}) => {
    const path = new URL(url).pathname.replace('/repos/o/r', '');
    const method = options.method ?? 'GET';
    const body = typeof options.body === 'string' ? JSON.parse(options.body) : null;
    if (path.includes('/actions/workflows/')) {
      const workflow = path.match(/\/actions\/workflows\/([^/]+)\/runs/)[1];
      const count = (workflowRequests.get(workflow) ?? 0) + 1;
      workflowRequests.set(workflow, count);
      const rerun = rerunOnRecheck && workflow !== 'native-release.yaml' && count >= 2;
      const rerunId = workflow === 'build.yaml' ? 22 : 23;
      const id = workflow === 'build.yaml' ? 10 : workflow === 'android.yaml' ? 11 : state.nativeId;
      const run = rerun
        ? { ...push, head_branch: branch, id: rerunId, run_attempt: 2, html_url: `https://github.com/o/r/actions/runs/${rerunId}` }
        : { ...push, head_branch: branch, id, run_number: state.nativeRunNumber,
          run_attempt: workflow === 'native-release.yaml' ? state.nativeAttempt : 1,
          html_url: `https://github.com/o/r/actions/runs/${id}` };
      return result({ workflow_runs: [run] });
    }
    if (path === `/git/ref/tags/${tag}`) return state.ref ? result(state.ref) : result(null, 404);
    if (path === '/releases' && method === 'GET') return result(state.release ? [state.release] : []);
    if (method !== 'GET') mutations.push(`${method} ${path}`);
    if (path === `/git/refs/tags/${tag}` && method === 'PATCH') {
      state.ref = { object: { type: 'commit', sha: body.sha } };
      return result(state.ref);
    }
    if (path === '/git/refs' && method === 'POST') return result(state.ref = { object: { type: 'commit', sha: body.sha } });
    if (path === '/releases' && method === 'POST') {
      state.release = { ...body, id: 1, assets: [], upload_url: 'https://uploads.github.com/repos/o/r/releases/1/assets{?name,label}' };
      return result(state.release);
    }
    if (path === '/releases/1/assets' && method === 'POST') {
      const name = new URL(url).searchParams.get('name');
      const asset = { id: nextAssetId++, name, state: 'uploaded',
        digest: `sha256:${createHash('sha256').update(options.body).digest('hex')}` };
      state.release.assets.push(asset);
      state.assetContents.set(name, Buffer.from(options.body));
      return result(asset);
    }
    if (path.startsWith('/releases/assets/') && method === 'DELETE') {
      const deleted = state.release.assets.find(asset => asset.id === Number(path.split('/').pop()));
      state.assetContents.delete(deleted.name);
      state.release.assets = state.release.assets.filter(asset => asset.id !== Number(path.split('/').pop()));
      return result(null, 204);
    }
    if (path === '/releases/1' && method === 'GET') return result(state.release);
    if (path === '/releases/1' && method === 'PATCH') {
      Object.assign(state.release, body);
      return result(state.release);
    }
    throw new Error(`Unexpected request: ${method} ${url}`);
  };
  const download = async (_command, args) => {
    assert.equal(args[2], String(state.nativeId));
    const artifact = args[args.indexOf('--name') + 1];
    const directory = args[args.indexOf('--dir') + 1];
    const windows = artifact.includes('-windows-');
    const android = artifact.includes('-android-');
    const arch = artifact.includes('-arm64-') ? 'arm64' : 'x86_64';
    const name = android ? 'portal-client-android-multiabi.apk' : windows ? 'portalclient.exe' : 'Portal.AppImage';
    const contents = Buffer.from(artifact);
    const hash = createHash('sha256').update(contents).digest('hex');
    await writeFile(join(directory, name), contents);
    await writeFile(join(directory, 'SHA256SUMS'), `${hash}  ${name}\n`);
    await writeFile(join(directory, 'provenance.txt'),
      `commit=${sha}\nrun=${state.nativeId}\nattempt=${state.nativeAttempt}\nrunner=${android ? 'Linux-Android' : `${windows ? 'Windows' : 'Linux'}-${arch}`}\n${android ? 'abi=arm64-v8a,x86_64\n' : ''}version=${state.version}\nrust=rustc\nnode=v24\n`);
  };
  const options = { event: { workflow_run: { ...push, head_branch: branch, head_repository: { id: 123 } } }, repository: 'o/r', repositoryId: '123',
    apiUrl: 'https://api.github.com', token: 'test', publisherRunId: '1', request, download, tempRoot: dir };
  return { options, state, mutations, version, tag, cleanup: () => rm(dir, { recursive: true, force: true }) };
}

// @spec DELIVERY-008 DELIVERY-010 DELIVERY-011 DELIVERY-012 DELIVERY-021
test('publication stays draft after verification reruns and retries the same native attempt', async (t) => {
  const { options, state, mutations, version, cleanup } = await publicationFixture({ rerunOnRecheck: true });
  t.after(cleanup);
  await assert.rejects(publish(options), /Verification changed during publication/);
  assert.equal(state.release.draft, true);
  assert.equal(state.release.assets.length, 7);
  const provenanceDigest = state.release.assets.find(asset => asset.name.startsWith('provenance-')).digest;
  const originalDigest = state.release.assets[0].digest;
  state.release.assets[0].digest = 'sha256:incorrect';
  await assert.rejects(publish({ ...options, publisherRunId: 'mismatch' }), /Mismatched release asset/);
  state.release.assets[0].digest = originalDigest;
  await publish({ ...options, publisherRunId: '2' });
  assert.equal(state.ref.object.sha, sha);
  assert.equal(state.release.assets.find(asset => asset.name.startsWith('provenance-')).digest, provenanceDigest);
  assert.equal(state.release.draft, false);
  assert.equal(state.release.prerelease, true);
  assert.deepEqual(state.release.assets.map(a => a.name).sort(), [
    `SHA256SUMS-${version}-b12.1`,
    `portal-client-android-multiabi-${version}-b12.1.apk`,
    `portal-client-linux-arm64-${version}-b12.1.AppImage`,
    `portal-client-linux-x86_64-${version}-b12.1.AppImage`,
    `portal-client-windows-arm64-${version}-b12.1.exe`,
    `portal-client-windows-x86_64-${version}-b12.1.exe`,
    `provenance-${version}-b12.1.json`,
  ].sort());
  assert.equal(mutations.some(mutation => mutation.startsWith('DELETE ')), false);
  const before = mutations.length;
  await publish({ ...options, publisherRunId: '3' });
  assert.equal(mutations.length, before);
});

for (const channel of ['dev', 'stable']) {
  // @spec DELIVERY-010 DELIVERY-020 DELIVERY-021 DELIVERY-022
  test(`new ${channel} release presents versioned downloads and verifiable build details`, async (t) => {
    const { options, state, version, tag, cleanup } = await publicationFixture({ channel });
    t.after(cleanup);
    await publish(options);
    assert.equal(state.release.name, version);
    assert.equal(state.release.tag_name, tag);
    assert.equal(state.release.prerelease, channel === 'dev');
    assert.equal(state.release.draft, false);
    const suffix = `${version}-b12.1`;
    const binaries = [
      `portal-client-linux-x86_64-${suffix}.AppImage`,
      `portal-client-linux-arm64-${suffix}.AppImage`,
      `portal-client-windows-x86_64-${suffix}.exe`,
      `portal-client-windows-arm64-${suffix}.exe`,
      `portal-client-android-multiabi-${suffix}.apk`,
    ];
    const sumsName = `SHA256SUMS-${suffix}`;
    const provenanceName = `provenance-${suffix}.json`;
    assert.deepEqual(state.release.assets.map(asset => asset.name).sort(),
      [...binaries, sumsName, provenanceName].sort());
    const expectedSums = binaries.map(name =>
      `${createHash('sha256').update(state.assetContents.get(name)).digest('hex')}  ${name}\n`).join('');
    assert.equal(state.assetContents.get(sumsName).toString(), expectedSums);
    const provenance = JSON.parse(state.assetContents.get(provenanceName));
    assert.equal(provenance.commit, sha);
    assert.equal(provenance.nativeRun, '12');
    assert.equal(provenance.nativeRunNumber, 12);
    assert.equal(provenance.nativeAttempt, '1');
    assert.deepEqual(provenance.artifacts.map(artifact => artifact.asset), binaries);

    const body = state.release.body;
    assert.ok(body.includes(`commit ${sha}`));
    for (const [label, id] of [
      ['Build and publish', 10],
      ['Android debug smoke', 11],
      ['Native client release builds', 12],
    ]) assert.ok(body.includes(`[${label}](https://github.com/o/r/actions/runs/${id}) (attempt 1)`));
    assert.match(body, /check and save (?:a|your) Portal address/i);
    assert.match(body, /sign.in.*(?:catalog|sites)|(?:catalog|sites).*sign.in/i);
    assert.doesNotMatch(body, /placeholder and does not connect/i);
    assert.match(body, /unsigned/i);
    assert.match(body, /Linux:.*x86_64.*arm64/i);
    assert.match(body, /Windows:.*x86_64.*arm64/i);
    assert.match(body, /Android:.*signed.*arm64-v8a.*x86_64/i);
    assert.match(body, /WebView2/);
    assert.match(body, /AppImage\/FUSE/);
    assert.match(body, /arm64 has not been device-tested/i);
    assert.ok(body.includes('`b12.1` identifies native build 12, attempt 1.'));
    assert.ok(body.includes(`sha256sum --check --ignore-missing ${sumsName}`));
    assert.ok(body.includes('Get-FileHash'));
    assert.ok(body.includes('replacing `PATH_TO_FILE`'));
    assert.ok(body.includes(provenanceName));
  });
}

for (const [name, next] of [
  ['a rerun', { nativeId: 12, nativeRunNumber: 12, nativeAttempt: 2 }],
  ['a new run', { nativeId: 13, nativeRunNumber: 13, nativeAttempt: 1 }],
]) {
  // @spec DELIVERY-011 DELIVERY-018 DELIVERY-021
  test(`${name} of the same version retains the old release until new assets are verified`, async (t) => {
    const { options, state, version, cleanup } = await publicationFixture();
    t.after(cleanup);
    await publish(options);
    const oldBody = state.release.body;
    const oldNames = state.release.assets.map(asset => asset.name);
    Object.assign(state, next);
    let uploads = 0;
    const request = async (url, init) => {
      if (new URL(url).hostname === 'uploads.github.com' && ++uploads === 2) throw new Error('Upload failed');
      return options.request(url, init);
    };
    await assert.rejects(publish({ ...options, publisherRunId: 'failed-update', request }), /Upload failed/);
    assert.equal(state.release.body, oldBody);
    assert.ok(oldNames.every(oldName => state.release.assets.some(asset => asset.name === oldName)));

    await publish({ ...options, publisherRunId: 'retry-update' });
    const suffix = `${version}-b${next.nativeRunNumber}.${next.nativeAttempt}`;
    assert.ok(state.release.assets.every(asset => asset.name.includes(suffix)));
    assert.equal(state.release.assets.length, 7);
    assert.ok(oldNames.every(oldName => !state.release.assets.some(asset => asset.name === oldName)));
    assert.equal(state.ref.object.sha, sha);
  });
}

// @spec DELIVERY-021
test('a version unsafe for a release filename is rejected before publication', async (t) => {
  const { options, state, cleanup } = await publicationFixture();
  t.after(cleanup);
  state.version = '../unsafe-alpha';
  await assert.rejects(publish(options), /Invalid native artifact version/);
  assert.equal(state.release, null);
});

// @spec DELIVERY-011 DELIVERY-020
test('rolling release title changes only after the new assets are verified and repairs a stale name', async (t) => {
  const { options, state, mutations, version, cleanup } = await publicationFixture();
  t.after(cleanup);
  const oldBody = '<!-- native-run:11-attempt:1-run-number:11 -->\nPrevious development build.';
  state.release = {
    id: 1, tag_name: 'dev', name: 'Portal development build', draft: false, prerelease: true, body: oldBody,
    assets: [{ id: 100, name: 'previous.AppImage', state: 'uploaded', digest: 'sha256:old' }],
    upload_url: 'https://uploads.github.com/repos/o/r/releases/1/assets{?name,label}',
  };
  const request = async (url, init) => {
    if (new URL(url).hostname === 'uploads.github.com') throw new Error('Upload failed');
    return options.request(url, init);
  };
  await assert.rejects(publish({ ...options, publisherRunId: 'failed', request }), /Upload failed/);
  assert.equal(state.release.name, 'Portal development build');
  assert.equal(state.release.body, oldBody);
  assert.equal(state.release.assets.length, 1);

  await publish({ ...options, publisherRunId: 'update' });
  assert.equal(state.release.name, version);
  assert.equal(state.ref.object.sha, sha);
  assert.equal(state.release.assets.some(asset => asset.name === 'previous.AppImage'), false);

  state.release.name = 'Portal development build';
  const before = mutations.length;
  await publish({ ...options, publisherRunId: 'name-only' });
  assert.equal(state.release.name, version);
  assert.deepEqual(mutations.slice(before), ['PATCH /releases/1']);

  const after = mutations.length;
  await publish({ ...options, publisherRunId: 'unchanged' });
  assert.equal(mutations.length, after);
});
