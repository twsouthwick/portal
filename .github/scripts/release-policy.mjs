const stableTagPattern = /^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/;
const versionPattern = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z.-]+))?$/;

export function releaseChannel(run) {
  if (run?.event !== 'push') return null;
  if (run.head_branch === 'main') return 'dev';
  return stableTagPattern.test(run.head_branch ?? '') ? 'stable' : null;
}

export function containerTags(version, channel) {
  const match = versionPattern.exec(version);
  if (!match) throw new Error(`Invalid MinVer version: ${version}`);
  const [, major, minor, patch, prerelease] = match;
  if (channel === 'stable') {
    if (prerelease) throw new Error('Expected stable version');
    return [version, `${major}.${minor}`, major, 'latest'];
  }
  if (channel === 'dev') {
    if (!prerelease) throw new Error('Expected prerelease version');
    return [version, 'dev'];
  }
  throw new Error(`Unknown release channel: ${channel}`);
}

export function isNewerRun(candidate, current) {
  const candidateNumber = Number(candidate?.run_number);
  const currentNumber = Number(current?.run_number);
  return Number.isSafeInteger(candidateNumber) && candidateNumber > currentNumber;
}
