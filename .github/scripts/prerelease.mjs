import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { readFile, readdir, mkdir, lstat } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, basename } from 'node:path';
import { pathToFileURL } from 'node:url';
import { isMinVerVersion, releaseChannel } from './release-policy.mjs';

const workflows = [
  { file: 'build.yaml', name: 'Build and publish' },
  { file: 'android.yaml', name: 'Android debug smoke' },
  { file: 'native-release.yaml', name: 'Native client release builds' },
];
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
  if (channel !== 'dev' && channel !== 'stable') throw new Error(`Unknown release channel: ${channel}`);
  if (!release) return 'create';
  if (channel === 'dev') {
    if (release.prerelease !== true) throw new Error('Refusing to modify a stable release');
  } else if (release.prerelease !== false) {
    throw new Error('Stable tag points to a prerelease');
  }
  if (!release.draft) return 'complete';
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

// @spec DELIVERY-008 DELIVERY-009 DELIVERY-010 DELIVERY-011 DELIVERY-012 DELIVERY-017 DELIVERY-018 DELIVERY-020 DELIVERY-021 DELIVERY-022
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
        const result = await api(`/actions/workflows/${workflow.file}/runs?event=push&head_sha=${sha}&per_page=100&page=${page}`);
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
  if (!Number.isSafeInteger(native.run_attempt) || native.run_attempt < 1) throw new Error('Missing native workflow attempt');
  const runNumber = String(native.run_number);
  const marker = `<!-- native-run:${runId}-attempt:${attempt}-run-number:${runNumber} -->`;
  async function findPublication(tag) {
    const ref = await api(`/git/ref/tags/${tag}`, {}, true);
    if (ref && await tagCommitSha(ref) !== sha) throw new Error('Conflicting tag');
    const releases = [];
    for (let page = 1; ; page++) {
      const result = await api(`/releases?per_page=100&page=${page}`);
      releases.push(...result);
      if (result.length < 100) break;
    }
    const release = findRelease(releases, tag);
    if (release && !ref) throw new Error('Release exists without its expected tag');
    return { ref, release, disposition: releaseDisposition(release, marker, channel) };
  }
  let publication = channel === 'stable' ? await findPublication(trigger.head_branch) : null;
  if (publication?.disposition === 'complete') {
    console.log(`Stable release ${trigger.head_branch} already exists; leaving it unchanged.`);
    return;
  }

  const root = join(tempRoot, `portal-release-${publisherRunId}`);
  await mkdir(root, { recursive: true });
  const downloaded = [];
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
    downloaded.push({ target, name, artifact, contents, hash, version: details.version, details: originalProvenance.trim() });
  }
  const versions = new Set(downloaded.map(item => item.version));
  if (versions.size !== 1) throw new Error('Native artifacts have different application versions');
  const version = versions.values().next().value;
  if (!isMinVerVersion(version)) throw new Error('Invalid native artifact version');
  if (channel === 'stable' && version !== trigger.head_branch.slice(1)) throw new Error('Native version does not match stable tag');
  if (channel === 'dev' && !version.includes('-')) throw new Error('Development artifacts must use a MinVer prerelease version');
  const tag = channel === 'dev' ? version : trigger.head_branch;
  publication ??= await findPublication(tag);
  if (publication.disposition === 'complete') {
    console.log(`Release ${tag} already exists; leaving it unchanged.`);
    return;
  }
  let { ref, release, disposition } = publication;
  const suffix = `${version}-b${runNumber}.${attempt}`;
  const assets = [];
  const provenance = [];
  for (const item of downloaded) {
    const assetName = `portal-client-${item.target.os}-${item.target.arch}-${suffix}${item.target.extension}`;
    assets.push({ name: assetName, contents: item.contents, hash: item.hash });
    provenance.push({ asset: assetName, originalName: item.name, artifact: item.artifact, version: item.version, details: item.details });
  }
  const sumsName = `SHA256SUMS-${suffix}`;
  const provenanceName = `provenance-${suffix}.json`;
  const sums = assets.map(asset => `${asset.hash}  ${asset.name}`).join('\n') + '\n';
  assets.push({ name: sumsName, contents: Buffer.from(sums) });
  assets.push({ name: provenanceName, contents: Buffer.from(JSON.stringify({
    commit: sha, nativeRun: runId, nativeAttempt: attempt, nativeRunNumber: native.run_number,
    artifacts: provenance,
  }, null, 2) + '\n') });
  for (const asset of assets) asset.hash = createHash('sha256').update(asset.contents).digest('hex');
  const body = [
    marker, channel === 'stable' ? `Stable release ${trigger.head_branch} for commit ${sha}.` : `Development build for commit ${sha}.`, '',
    'The native client can check and save a Portal address. Sign-in and the site catalog are not available yet.', '',
    '## Downloads',
    '- **Linux:** Choose `x86_64` (Intel/AMD) or `arm64`. AppImages are unsigned and require compatible system libraries and AppImage/FUSE support (or extraction).',
    '- **Windows:** Choose `x86_64` (Intel/AMD) or `arm64`. Executables are unsigned, have no installer, and require the WebView2 runtime.',
    '- **Android:** The signed `multiabi` APK supports arm64-v8a and x86_64. Only x86_64 is emulator-tested; arm64 has not been device-tested.', '',
    `\`b${runNumber}.${attempt}\` identifies native build ${runNumber}, attempt ${attempt}.`, '',
    '## Verify your download',
    `Download \`${sumsName}\` and compare your file's SHA-256 hash with its entry:`,
    `- Linux: Run \`sha256sum --check --ignore-missing ${sumsName}\` in the same directory as the download.`,
    '- Windows: Run `(Get-FileHash -Algorithm SHA256 "PATH_TO_FILE").Hash.ToLowerInvariant()` in PowerShell, replacing `PATH_TO_FILE` with your download, and compare it with the matching line.',
    `- Build details: \`${provenanceName}\`. Checksums and provenance are generated by CI, not independently signed.`, '',
    '## Verification',
    ...runs.map((run, index) => `- [${workflows[index].name}](${run.html_url}) (attempt ${run.run_attempt})`),
  ].join('\n');

  if (disposition === 'create') {
    if (!ref) {
      await api('/git/refs', { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ref: `refs/tags/${tag}`, sha }) });
      ref = await api(`/git/ref/tags/${tag}`);
      if (await tagCommitSha(ref) !== sha) throw new Error('Created tag does not point at the source commit');
    }
    release = await api('/releases', { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tag_name: tag, target_commitish: trigger.head_branch, name: version, body, draft: true, prerelease: channel === 'dev', make_latest: 'false' }) });
  }
  const uploadUrl = new URL(release.upload_url.replace(/\{.*$/, ''));
  if (uploadUrl.protocol !== 'https:' || uploadUrl.hostname !== 'uploads.github.com') throw new Error('Unexpected release upload host');
  function checkRelease(current) {
    if (current.tag_name !== tag || current.prerelease !== (channel === 'dev')) throw new Error('Release changed during publication');
    if (!current.draft || !current.body?.includes(marker)) throw new Error('Draft changed during publication');
    for (const uploaded of current.assets) {
      const expected = assets.find(asset => asset.name === uploaded.name);
      if (expected && (uploaded.state !== 'uploaded' || uploaded.digest !== `sha256:${expected.hash}`)) {
        throw new Error(`Mismatched release asset: ${uploaded.name}`);
      }
      if (!expected) throw new Error(`Unexpected release asset: ${uploaded.name}`);
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
  if (finalTagSha !== sha) throw new Error('Tag changed during publication');
  const finalRelease = await api(`/releases/${release.id}`);
  checkRelease(finalRelease);
  if (assets.some(asset => !finalRelease.assets.some(uploaded => uploaded.name === asset.name && uploaded.digest === `sha256:${asset.hash}`))) {
    throw new Error('Release is missing a verified asset');
  }
  await api(`/releases/${release.id}`, { method: 'PATCH', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name: version, body, draft: false, prerelease: channel === 'dev', make_latest: channel === 'stable' ? 'true' : 'false' }) });
  console.log(`Published ${channel} release ${tag} for ${sha}.`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const event = JSON.parse(await readFile(process.env.GITHUB_EVENT_PATH, 'utf8'));
  publish({ event, repository: process.env.GITHUB_REPOSITORY, repositoryId: process.env.GITHUB_REPOSITORY_ID,
    apiUrl: process.env.GITHUB_API_URL, token: process.env.GH_TOKEN, publisherRunId: process.env.GITHUB_RUN_ID })
    .catch(error => { console.error(error); process.exitCode = 1; });
}
