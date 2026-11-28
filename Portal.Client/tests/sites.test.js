import assert from 'node:assert/strict';
import { it } from 'node:test';
import { appsFor, badges, groupSites, iconClass, isWebUrl, statusLine } from '../src/sites.js';

const site = (id, categories = [], extra = {}) => ({ id, name: id, categories, apps: [], ...extra });

// @spec CLIENT-037
it('groups sites by category in order of first appearance with Other last', () => {
  const groups = groupSites([
    site('jellyfin', ['media']),
    site('wiki'),
    site('photos', ['media', 'family']),
    site('calendar', ['family']),
  ]);
  assert.deepEqual(groups.map(g => [g.category, g.sites.map(s => s.id)]), [
    ['media', ['jellyfin', 'photos']],
    ['family', ['photos', 'calendar']],
    ['Other', ['wiki']],
  ]);
});

// @spec CLIENT-038
it('accepts only bootstrap icon classes and falls back to bi-globe', () => {
  assert.equal(iconClass('bi bi-headphones'), 'bi bi-headphones');
  assert.equal(iconClass('bi-film'), 'bi bi-film');
  for (const icon of [undefined, '', 'fa fa-film', 'bi bi-Film', 'bi-x" onclick="', 'bi bi-<b>']) {
    assert.equal(iconClass(icon), 'bi bi-globe', String(icon));
  }
});

// @spec CLIENT-038
it('labels private and internal sites', () => {
  assert.deepEqual(badges(site('a', [], { isPrivate: true, isInternal: true })), ['Private', 'Internal']);
  assert.deepEqual(badges(site('a')), []);
});

// @spec CLIENT-039 CLIENT-040
it('treats only http and https as web links', () => {
  assert.equal(isWebUrl('https://media.example'), true);
  assert.equal(isWebUrl('http://media.example/x'), true);
  for (const url of [undefined, '', 'javascript:alert(1)', 'mailto:a@b', 'relative/path', 'plex://x']) {
    assert.equal(isWebUrl(url), false, String(url));
  }
});

// @spec CLIENT-041
it('lists only apps for the current platform with safe actions', () => {
  const apps = [
    { name: 'Droid', scheme: 'abs://', store: 'Google', storeId: 'https://play.google.com/x' },
    { name: 'Win', scheme: 'win://', store: 'microsoft', storeId: '9NBLGGH' },
    { name: 'Win2', scheme: 'javascript:alert(1)', store: 'windows', storeId: 'https://apps.microsoft.com/x' },
    { name: 'Tux', scheme: 'tux://', store: 'linux', storeId: 'https://flathub.org/x' },
    { name: 'Mac', scheme: 'mac://', store: 'apple', storeId: 'https://apps.apple.com/x' },
  ];
  assert.deepEqual(appsFor(apps, 'android'), [{ name: 'Droid', launch: 'abs://', storeUrl: 'https://play.google.com/x' }]);
  assert.deepEqual(appsFor(apps, 'windows'), [
    { name: 'Win', launch: 'win://', storeUrl: null },
    { name: 'Win2', launch: null, storeUrl: 'https://apps.microsoft.com/x' },
  ]);
  assert.deepEqual(appsFor(apps, 'linux').map(a => a.name), ['Tux']);
  assert.deepEqual(appsFor(apps, 'macos'), []);
});

// @spec CLIENT-036
it('describes each refresh status', () => {
  const now = 1_000_000;
  assert.deepEqual(statusLine({ status: 'refreshing' }, now), { state: 'refreshing', text: 'Refreshing…' });
  assert.deepEqual(statusLine({ status: 'upToDate', refreshedAt: now - 30 }, now), { state: 'ok', text: 'Up to date · updated just now' });
  assert.deepEqual(statusLine({ status: 'offline', refreshedAt: now - 7200 }, now), { state: 'warn', text: 'Offline · updated 2 hours ago' });
  assert.deepEqual(statusLine({ status: 'failed', refreshedAt: now - 120 }, now), { state: 'warn', text: "Couldn't refresh · updated 2 minutes ago" });
  assert.deepEqual(statusLine({ status: 'failed' }, now), { state: 'warn', text: "Couldn't refresh" });
  assert.deepEqual(statusLine({ status: 'signInRequired' }, now), { state: 'error', text: 'Sign-in required' });
});
