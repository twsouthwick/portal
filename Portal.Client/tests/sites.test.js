import assert from 'node:assert/strict';
import { it } from 'node:test';
import { readFile } from 'node:fs/promises';
import { runInNewContext } from 'node:vm';
import { appsFor, badges, groupSites, iconClass, isWebUrl, statusLine } from '../src/sites.js';

const site = (id, categories = [], extra = {}) => ({ id, name: id, categories, apps: [], ...extra });

async function catalogPage(manifest, { windowsLineEndings = false } = {}) {
  let activeElement;
  const nodes = [];
  function element(tag = 'div') {
    const node = {
      tag, children: [], dataset: {}, hidden: false, attributes: {}, listeners: {},
      setAttribute(name, value) {
        this.attributes[name] = value;
        if (name.startsWith('data-')) this.dataset[name.slice(5).replace(/-([a-z])/g, (_, c) => c.toUpperCase())] = value;
      },
      addEventListener(name, handler) { this.listeners[name] = handler; },
      append(...children) { this.children.push(...children); },
      replaceChildren(...children) { this.children = []; this.append(...children); },
      focus() { activeElement = this; },
      click() { return this.listeners.click?.({ currentTarget: this }); },
      get textContent() { return this.text ?? this.children.map(child => typeof child === 'string' ? child : child.textContent).join(''); },
      set textContent(value) { this.text = value; this.children = []; },
      querySelectorAll(selector) {
        const result = [];
        const visit = node => {
          for (const child of node.children) {
            if (typeof child === 'string') continue;
            if (selector.startsWith('.') && child.className?.split(' ').includes(selector.slice(1))) result.push(child);
            visit(child);
          }
        };
        visit(this);
        return result;
      },
      querySelector(selector) {
        const match = /^\[data-site-id="(.*)"\]$/.exec(selector);
        return this.querySelectorAll('.site-card').find(card => card.dataset.siteId === match?.[1]);
      },
    };
    nodes.push(node);
    return node;
  }
  const ids = [
    'onboarding-view', 'portal-view', 'portal-banner', 'portal-banner-text', 'portal-banner-cancel',
    'signin-panel', 'signin-title', 'signin-origin', 'signin-button', 'change-address', 'sites-panel',
    'manifest-name', 'manifest-description', 'refresh-status', 'refresh-button', 'header-signin',
    'signout-button', 'site-groups', 'site-details',
  ];
  const elements = Object.fromEntries(ids.map(id => [id, element()]));
  let currentManifest = manifest;
  const window = { __TAURI__: { core: { invoke: async command => {
    if (command === 'saved_portal_url') return null;
    if (command === 'platform') return 'android';
    if (command === 'sign_out') return { status: 'signedOut', manifest: null };
    if (command === 'session_startup' || command === 'refresh_manifest') {
      return { status: 'upToDate', manifest: currentManifest };
    }
    throw new Error(`Unexpected command: ${command}`);
  } } } };
  const source = await readFile(new URL('../src/portal.js', import.meta.url), 'utf8');
  const script = (windowsLineEndings ? source.replace(/\r?\n/g, '\r\n') : source).replace(/^import .*;\r?\n/gm, '');
  runInNewContext(script, {
    appsFor, badges, groupSites, iconClass, isWebUrl, statusLine,
    CSS: { escape: value => value },
    document: {
      getElementById: id => elements[id] ?? nodes.find(node => node.attributes.id === id),
      createElement: element,
      addEventListener() {},
    },
    window,
  });
  await window.portalApp.showPortal('https://portal.example');
  return {
    elements, nodes, showPortal: window.portalApp.showPortal,
    get activeElement() { return activeElement; },
    setManifest(value) { currentManifest = value; },
  };
}

// @spec CLIENT-041 CLIENT-048
it('runs category navigation with Windows line endings in the client script', async () => {
  const page = await catalogPage({ name: 'Portal', sites: [site('photos', ['media'])] }, { windowsLineEndings: true });
  const groups = page.elements['site-groups'];
  groups.querySelectorAll('.category-card')[0].click();
  assert.equal(groups.querySelectorAll('.site-card')[0].dataset.siteId, 'photos');
});

// @spec CLIENT-041 CLIENT-048
it('starts with categories and drills into one list, returning with keyboard focus', async () => {
  const page = await catalogPage({ name: 'Portal', sites: [
    site('photos', ['media', 'family']), site('films', ['media']), site('wiki'),
  ] });
  const groups = page.elements['site-groups'];
  const categories = groups.querySelectorAll('.category-card');
  assert.deepEqual(categories.map(card => card.dataset.category), ['media', 'family', 'Other']);
  assert.match(categories[0].textContent, /2 sites/);
  assert.equal(groups.querySelectorAll('.site-card').length, 0);
  categories[1].click();
  assert.deepEqual(groups.querySelectorAll('.site-card').map(card => card.dataset.siteId), ['photos']);
  assert.equal(page.activeElement.attributes.id, 'categories-back');
  groups.querySelectorAll('.site-card')[0].click();
  assert.equal(groups.hidden, true);
  assert.equal(page.activeElement.attributes.id, 'details-back');
  page.nodes.find(node => node.attributes.id === 'details-back').click();
  assert.equal(groups.hidden, false);
  assert.equal(page.activeElement.dataset.siteId, 'photos');
  groups.querySelectorAll('.back-button')[0].click();
  assert.equal(groups.querySelectorAll('.site-card').length, 0);
  assert.equal(page.activeElement.dataset.category, 'family');
});

// @spec CLIENT-048
it('refresh preserves valid selections and returns to a valid parent when membership changes', async () => {
  const page = await catalogPage({ name: 'Portal', sites: [site('photos', ['media']), site('films', ['media'])] });
  const groups = page.elements['site-groups'];
  groups.querySelectorAll('.category-card')[0].click();
  groups.querySelectorAll('.site-card')[0].click();
  await page.elements['refresh-button'].click();
  assert.equal(page.elements['site-details'].hidden, false);
  page.setManifest({ name: 'Portal', sites: [site('photos', ['family']), site('films', ['media'])] });
  await page.elements['refresh-button'].click();
  assert.equal(page.elements['site-details'].hidden, true);
  assert.deepEqual(groups.querySelectorAll('.site-card').map(card => card.dataset.siteId), ['films']);
  page.setManifest({ name: 'Portal', sites: [site('photos', ['family'])] });
  await page.elements['refresh-button'].click();
  assert.equal(groups.querySelectorAll('.site-card').length, 0);
  assert.equal(groups.querySelectorAll('.category-card')[0].dataset.category, 'family');
  groups.querySelectorAll('.category-card')[0].click();
  await page.elements['signout-button'].click();
  await page.showPortal('https://portal.example');
  assert.equal(groups.querySelectorAll('.site-card').length, 0);
});

// @spec CLIENT-041
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

// @spec CLIENT-042
it('accepts only bootstrap icon classes and falls back to bi-globe', () => {
  assert.equal(iconClass('bi bi-headphones'), 'bi bi-headphones');
  assert.equal(iconClass('bi-film'), 'bi bi-film');
  for (const icon of [undefined, '', 'fa fa-film', 'bi bi-Film', 'bi-x" onclick="', 'bi bi-<b>']) {
    assert.equal(iconClass(icon), 'bi bi-globe', String(icon));
  }
});

// @spec CLIENT-042
it('labels private and internal sites', () => {
  assert.deepEqual(badges(site('a', [], { isPrivate: true, isInternal: true })), ['Private', 'Internal']);
  assert.deepEqual(badges(site('a')), []);
});

// @spec CLIENT-043 CLIENT-044
it('treats only http and https as web links', () => {
  assert.equal(isWebUrl('https://media.example'), true);
  assert.equal(isWebUrl('http://media.example/x'), true);
  for (const url of [undefined, '', 'javascript:alert(1)', 'mailto:a@b', 'relative/path', 'plex://x']) {
    assert.equal(isWebUrl(url), false, String(url));
  }
});

// @spec CLIENT-045
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

// @spec CLIENT-040
it('describes each refresh status', () => {
  const now = 1_000_000;
  assert.deepEqual(statusLine({ status: 'refreshing' }, now), { state: 'refreshing', text: 'Refreshing…' });
  assert.deepEqual(statusLine({ status: 'upToDate', refreshedAt: now - 30 }, now), { state: 'ok', text: 'Up to date · updated just now' });
  assert.deepEqual(statusLine({ status: 'offline', refreshedAt: now - 7200 }, now), { state: 'warn', text: 'Offline · updated 2 hours ago' });
  assert.deepEqual(statusLine({ status: 'failed', refreshedAt: now - 120 }, now), { state: 'warn', text: "Couldn't refresh · updated 2 minutes ago" });
  assert.deepEqual(statusLine({ status: 'failed' }, now), { state: 'warn', text: "Couldn't refresh" });
  assert.deepEqual(statusLine({ status: 'signInRequired' }, now), { state: 'error', text: 'Sign-in required' });
});
