import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { readFile, readdir, mkdir, lstat } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, basename } from 'node:path';
import { pathToFileURL } from 'node:url';
import { isNewerRun, releaseChannel } from './release-policy.mjs';

const workflows = ['build.yaml', 'android.yaml', 'native-release.yaml'];
const targets = [
  { os: 'linux', arch: 'x86_64', runner: 'Linux-x86_64', extension: '.AppImage' },
  { os: 'linux', arch: 'arm64', runner: 'Linux-arm64', extension: '.AppImage' },
  { os: 'windows', arch: 'x86_64', runner: 'Windows-x86_64', extension: '.exe' },
  { os: 'windows', arch: 'arm64', runner: 'Windows-arm64', extension: '.exe' },
  { os: 'android', arch: 'multiabi', runner: 'Linux-Android', extension: '.apk', abi: 'arm64-v8a,x86_64' },
];

export function qualifies(run, sha, branch = 'main') {
  return run.event === 'push' && run.head_branch === branch && run.head_sha === sha;
}

export function selectRun(runs, sha, branch = 'main') {
  const run = runs.filter(r => qualifies(r, sha, branch)).sort((a, b) => b.id - a.id)[0];
  return run?.status === 'completed' && run.conclusion === 'success' ? run : null;
}

export function releaseDisposition(release, marker, channel = 'stable') {
  if (!release) return 'create';
  if (channel === 'dev') {
    if (release.tag_name && release.tag_name !== 'dev') throw new Error('Refusing to modify a non-dev release');
    if (!release.prerelease) throw new Error('Refusing to modify a stable release');
    return release.body?.includes(marker) ? 'retry' : 'update';
  }
  if (channel !== 'stable') throw new Error(`Unknown release channel: ${channel}`);
  if (!release.draft) {
    if (release.prerelease) throw new Error('Stable tag points to a prerelease');
    return 'complete';
  }
  if (!release.body?.includes(marker)) throw new Error('Draft belongs to another native run or attempt');
  return 'retry';
}

export function findRelease(releases, tag) {
  const matches = releases.filter(release => release.tag_name === tag);
  if (matches.length > 1) throw new Error('Multiple releases use the same tag');
  return matches[0] ?? null;
}

export function validateArtifact(expected, contents, checksums, provenance) {
  if (basename(expected.name) !== expected.name || /[\r\n]/.test(expected.name)) throw new Error('Invalid artifact filename');
  const lines = checksums.trim().split(/\r?\n/);
  const match = /^([a-fA-F0-9]{64})  (?:\.\/)?(.+)$/.exec(lines[0]);
  const hash = createHash('sha256').update(contents).digest('hex');
  if (lines.length !== 1 || !match || match[1].toLowerCase() !== hash || match[2] !== expected.name) {
    throw new Error('Artifact checksum or filename mismatch');
  }
  const values = Object.fromEntries(provenance.trim().split(/\r?\n/).map(line => {
    const index = line.indexOf('=');
    if (index < 1) throw new Error('Malformed provenance');
    return [line.slice(0, index), line.slice(index + 1)];
  }));
  for (const [key, value] of Object.entries({ commit: expected.sha, run: expected.run, attempt: expected.attempt, runner: expected.runner })) {
    if (values[key] !== value) throw new Error(`Provenance mismatch: ${key}`);
  }
  if (expected.abi && values.abi !== expected.abi) throw new Error('Provenance mismatch: abi');
  if (expected.version && values.version !== expected.version) throw new Error('Provenance mismatch: version');
  if (!values.rust || !values.node || !values.version) throw new Error('Missing toolchain provenance');
  return hash;
}

function provenanceValues(provenance) {
  return Object.fromEntries(provenance.trim().split(/\r?\n/).map(line => {
    const index = line.indexOf('=');
    if (index < 1) throw new Error('Malformed provenance');
    return [line.slice(0, index), line.slice(index + 1)];
  }));
}

function runNumberFromBody(body) {
  const match = /<!-- native-run:\d+-attempt:\d+-run-number:(\d+) -->/.exec(body ?? '');
  return match ? Number(match[1]) : null;
}

// @spec DELIVERY-008 DELIVERY-009 DELIVERY-010 DELIVERY-011 DELIVERY-012 DELIVERY-017 DELIVERY-018
export async function publish({ event, repository, repositoryId, apiUrl, token, publisherRunId, request = fetch, download = execFileSync, tempRoot = tmpdir() }) {
  const trigger = event.workflow_run;
  const sha = trigger.head_sha;
  const channel = releaseChannel(trigger);
  if (!/^[a-f0-9]{40}$/.test(sha) || !channel || !qualifies(trigger, sha, trigger.head_branch) ||
      String(trigger.head_repository?.id) !== String(repositoryId)) throw new Error('Untrusted trigger');

  const repo = repository;
  const apiBase = `${apiUrl}/repos/${repo}`;
  const headers = { Authorization: ['Bearer', token].join(' '), Accept: 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28' };
  async function api(url, options = {}, allow404 = false) {
    const response = await request(url.startsWith('https://') ? url : `${apiBase}${url}`, {
      ...options,
      headers: { ...headers, ...options.headers },
    });
    if (allow404 && response.status === 404) return null;
    if (!response.ok) throw new Error(`GitHub API ${response.status}: ${await response.text()}`);
    if (response.status === 204) return null;
    return response.json();
  }
  async function latestRuns() {
    const selected = [];
    for (const workflow of workflows) {
      const runs = [];
      for (let page = 1; ; page++) {
        const result = await api(`/actions/workflows/${workflow}/runs?event=push&head_sha=${sha}&per_page=100&page=${page}`);
        runs.push(...result.workflow_runs);
        if (result.workflow_runs.length < 100) break;
      }
      const run = selectRun(runs, sha, trigger.head_branch);
      if (!run) return null;
      selected.push(run);
    }
    return selected;
  }
  async function tagCommitSha(ref) {
    if (ref.object.type === 'commit') return ref.object.sha;
    if (ref.object.type === 'tag') {
      const tag = await api(`/git/tags/${ref.object.sha}`);
      if (tag.object?.type === 'commit') return tag.object.sha;
    }
    throw new Error('Tag does not point directly to a commit');
  }

  const runs = await latestRuns();
  if (!runs) {
    console.log(`Waiting for all required ${channel} workflows to succeed for ${sha}.`);
    return;
  }
  const native = runs[2];
  const runId = String(native.id);
  const attempt = String(native.run_attempt);
  if (!Number.isSafeInteger(native.run_number) || native.run_number < 1) throw new Error('Missing native workflow run number');
  const runNumber = String(native.run_number);
  const tag = channel === 'dev' ? 'dev' : trigger.head_branch;
  const marker = `<!-- native-run:${runId}-attempt:${attempt}-run-number:${runNumber} -->`;
  let ref = await api(`/git/ref/tags/${tag}`, {}, true);
  if (ref && channel === 'stable' && await tagCommitSha(ref) !== sha) throw new Error('Conflicting tag');
  const releases = [];
  for (let page = 1; ; page++) {
    const result = await api(`/releases?per_page=100&page=${page}`);
    releases.push(...result);
    if (result.length < 100) break;
  }
  let release = findRelease(releases, tag);
  if (release && !ref) throw new Error('Release exists without its expected tag');
  const currentRunNumber = runNumberFromBody(release?.body);
  if (channel === 'dev' && currentRunNumber !== null && isNewerRun({ run_number: currentRunNumber }, native)) {
    console.log(`A newer successful main run already published ${tag}; leaving it unchanged.`);
    return;
  }
  const disposition = releaseDisposition(release, marker, channel);
  if (disposition === 'complete') {
    console.log(`Stable release ${tag} already exists; leaving it unchanged.`);
    return;
  }

  const root = join(tempRoot, `portal-release-${publisherRunId}`);
  await mkdir(root, { recursive: true });
  const assets = [];
  const provenance = [];
  for (const target of targets) {
    const artifact = `portal-client-${target.os}-${target.arch}-${sha}-attempt-${attempt}`;
    const directory = join(root, artifact);
    await mkdir(directory);
    await download('gh', ['run', 'download', runId, '--repo', repo, '--name', artifact, '--dir', directory], { stdio: 'inherit' });
    const entries = await readdir(directory);
    const binaries = entries.filter(name => name.endsWith(target.extension));
    if (entries.length !== 3 || binaries.length !== 1 || !entries.includes('SHA256SUMS') || !entries.includes('provenance.txt')) {
      throw new Error(`Unexpected artifact contents: ${artifact}`);
    }
    for (const entry of entries) {
      if (!(await lstat(join(directory, entry))).isFile()) throw new Error(`Non-regular artifact: ${entry}`);
    }
    const name = binaries[0];
    const contents = await readFile(join(directory, name));
    const originalProvenance = await readFile(join(directory, 'provenance.txt'), 'utf8');
    const details = provenanceValues(originalProvenance);
    const hash = validateArtifact({ sha, run: runId, attempt, runner: target.runner, abi: target.abi, version: details.version, name }, contents,
      await readFile(join(directory, 'SHA256SUMS'), 'utf8'), originalProvenance);
    const assetName = `portal-client-${target.os}-${target.arch}-${sha}-run-${runId}-attempt-${attempt}${target.extension}`;
    assets.push({ name: assetName, contents, hash });
    provenance.push({ asset: assetName, originalName: name, artifact, version: details.version, details: originalProvenance.trim() });
  }
  const sumsName = `SHA256SUMS-${sha}-run-${runId}-attempt-${attempt}`;
  const provenanceName = `provenance-${sha}-run-${runId}-attempt-${attempt}.json`;
  const sums = assets.map(asset => `${asset.hash}  ${asset.name}`).join('\n') + '\n';
  assets.push({ name: sumsName, contents: Buffer.from(sums) });
  assets.push({ name: provenanceName, contents: Buffer.from(JSON.stringify({
    commit: sha, nativeRun: runId, nativeAttempt: attempt, nativeRunNumber: native.run_number,
    artifacts: provenance,
  }, null, 2) + '\n') });
  for (const asset of assets) asset.hash = createHash('sha256').update(asset.contents).digest('hex');
  const versions = new Set(provenance.map(item => item.version));
  if (versions.size !== 1) throw new Error('Native artifacts have different application versions');
  const version = versions.values().next().value;
  if (channel === 'stable' && version !== trigger.head_branch.slice(1)) throw new Error('Native version does not match stable tag');
  if (channel === 'dev' && !version.includes('-')) throw new Error('Development artifacts must use a MinVer prerelease version');
  const body = [
    marker, channel === 'stable' ? `Stable release ${trigger.head_branch} for commit ${sha}.` : `Development build for commit ${sha}.`, '',
    ...runs.map(run => `- Verification: ${run.html_url} (attempt ${run.run_attempt})`), '',
    'Linux AppImages and Windows executables are unsigned; Windows requires the WebView2 runtime.',
    'Linux requires compatible system libraries and AppImage/FUSE support (or extraction).',
    `The signed Android APK includes arm64-v8a and x86_64; only x86_64 is emulator-tested. arm64 has not been device-tested.`,
    'The native client is a placeholder and does not connect to Portal.', '',
    `Verify ${sumsName} before use. Build details are in ${provenanceName}.`,
    `Artifact version: ${version}.`,
  ].join('\n');

  if (disposition === 'create') {
    if (!ref) {
      await api('/git/refs', { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ref: `refs/tags/${tag}`, sha }) });
      ref = await api(`/git/ref/tags/${tag}`);
      if (await tagCommitSha(ref) !== sha) throw new Error('Created tag does not point at the source commit');
    }
    release = await api('/releases', { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tag_name: tag, target_commitish: trigger.head_branch, name: channel === 'stable' ? `Portal ${trigger.head_branch}` : 'Portal development build', body, draft: true, prerelease: channel === 'dev', make_latest: 'false' }) });
  }
  const uploadUrl = new URL(release.upload_url.replace(/\{.*$/, ''));
  if (uploadUrl.protocol !== 'https:' || uploadUrl.hostname !== 'uploads.github.com') throw new Error('Unexpected release upload host');
  function checkRelease(current) {
    if (current.tag_name !== tag || current.prerelease !== (channel === 'dev')) throw new Error('Release changed during publication');
    if (channel === 'stable' && (!current.draft || !current.body?.includes(marker))) {
      throw new Error('Stable draft changed during publication');
    }
    if (channel === 'dev') {
      const publishedRun = runNumberFromBody(current.body);
      if (publishedRun !== null && isNewerRun({ run_number: publishedRun }, native)) throw new Error('A newer main run already published dev');
    }
    for (const uploaded of current.assets) {
      const expected = assets.find(asset => asset.name === uploaded.name);
      if (expected && (uploaded.state !== 'uploaded' || uploaded.digest !== `sha256:${expected.hash}`)) {
        throw new Error(`Mismatched release asset: ${uploaded.name}`);
      }
      if (channel === 'stable' && !expected) throw new Error(`Unexpected stable release asset: ${uploaded.name}`);
    }
  }
  for (const asset of assets) {
    const current = await api(`/releases/${release.id}`);
    checkRelease(current);
    const existing = current.assets.find(uploaded => uploaded.name === asset.name);
    if (existing) {
      if (existing.digest !== `sha256:${asset.hash}`) throw new Error(`Mismatched release asset: ${asset.name}`);
      continue;
    }
    const url = new URL(uploadUrl);
    url.searchParams.set('name', asset.name);
    await api(url.toString(), { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: asset.contents });
  }
  const finalRuns = await latestRuns();
  if (!finalRuns || finalRuns.some((run, i) => run.id !== runs[i].id || run.run_attempt !== runs[i].run_attempt)) {
    throw new Error('Verification changed during publication; release remains a draft');
  }
  const finalRef = await api(`/git/ref/tags/${tag}`);
  if (!finalRef) throw new Error('Tag changed during publication');
  const finalTagSha = await tagCommitSha(finalRef);
  if (channel === 'stable' && finalTagSha !== sha) throw new Error('Stable tag changed during publication');
  const finalRelease = await api(`/releases/${release.id}`);
  checkRelease(finalRelease);
  if (assets.some(asset => !finalRelease.assets.some(uploaded => uploaded.name === asset.name && uploaded.digest === `sha256:${asset.hash}`))) {
    throw new Error('Release is missing a verified asset');
  }
  if (finalRelease.body !== body || finalRelease.draft || finalRelease.prerelease !== (channel === 'dev')) {
    await api(`/releases/${release.id}`, { method: 'PATCH', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ body, draft: false, prerelease: channel === 'dev', make_latest: channel === 'stable' ? 'true' : 'false' }) });
  }
  if (channel === 'dev' && finalTagSha !== sha) {
    await api(`/git/refs/tags/${tag}`, { method: 'PATCH', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sha, force: true }) });
    const updatedRef = await api(`/git/ref/tags/${tag}`);
    if (!updatedRef || await tagCommitSha(updatedRef) !== sha) throw new Error('Updated dev tag does not point at the source commit');
  }
  if (channel === 'dev') {
    const current = await api(`/releases/${release.id}`);
    for (const stale of current.assets.filter(uploaded => !assets.some(asset => asset.name === uploaded.name))) {
      await api(`/releases/assets/${stale.id}`, { method: 'DELETE' });
    }
  }
  console.log(`Published ${channel} release ${tag} for ${sha}.`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const event = JSON.parse(await readFile(process.env.GITHUB_EVENT_PATH, 'utf8'));
  publish({ event, repository: process.env.GITHUB_REPOSITORY, repositoryId: process.env.GITHUB_REPOSITORY_ID,
    apiUrl: process.env.GITHUB_API_URL, token: process.env.GH_TOKEN, publisherRunId: process.env.GITHUB_RUN_ID })
    .catch(error => { console.error(error); process.exitCode = 1; });
}
