import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { runInNewContext } from 'node:vm';
import { it } from 'node:test';

const script = await readFile(new URL('../src/main.js', import.meta.url), 'utf8');

function element() {
  const listeners = {};
  return {
    dataset: {},
    hidden: false,
    textContent: '',
    value: '',
    addEventListener(type, listener) { listeners[type] = listener; },
    dispatch(type) { return listeners[type]({ preventDefault() {} }); },
  };
}

async function page({
  check,
  save = async origin => origin,
  appVersion = async () => '1.2.3',
  savedVersion = async () => null,
}) {
  const elements = Object.fromEntries([
    'portal-url-form', 'portal-url', 'portal-status', 'portal-confirm', 'portal-save', 'portal-saved',
    'app-version', 'website-version',
  ].map(id => [id, element()]));
  elements['portal-confirm'].hidden = true;
  elements['portal-saved'].hidden = true;
  elements['website-version'].hidden = true;

  const invoke = async (command, args) => {
    if (command === 'saved_portal_url') return null;
    if (command === 'app_version') return appVersion();
    if (command === 'saved_portal_version') return savedVersion();
    if (command === 'check_portal_url') return check(args.input);
    if (command === 'save_portal_url') return save(args.origin);
    throw new Error(`Unexpected command: ${command}`);
  };
  runInNewContext(script, {
    document: { getElementById: id => elements[id] },
    window: { __TAURI__: { core: { invoke } } },
  });
  await new Promise(resolve => setTimeout(resolve));
  return elements;
}

// @spec CLIENT-011 CLIENT-013 CLIENT-028
it('distinguishes checking and success, then clears the result when the address changes', async () => {
  let resolveCheck;
  const checked = new Promise(resolve => { resolveCheck = resolve; });
  const elements = await page({ check: () => checked });
  const input = elements['portal-url'];
  const status = elements['portal-status'];
  const confirmation = elements['portal-confirm'];

  input.value = 'portal.example.com';
  const pending = elements['portal-url-form'].dispatch('submit');
  assert.equal(status.dataset.state, 'checking');
  assert.match(status.textContent, /Checking/);
  assert.equal(confirmation.hidden, true);

  resolveCheck({ origin: 'https://portal.example.com', outcome: { kind: 'signInRequired' } });
  await pending;
  assert.equal(status.dataset.state, 'success');
  assert.match(status.textContent, /Digital Fortress/);
  assert.match(status.textContent, /Sign-in is not available/);
  assert.equal(confirmation.hidden, false);

  input.value = 'other.example.com';
  input.dispatch('input');
  assert.equal(status.textContent, '');
  assert.equal(confirmation.hidden, true);
});

// @spec CLIENT-012 CLIENT-013 CLIENT-028
it('distinguishes errors and does not restore an outdated result', async () => {
  let resolveCheck;
  const checked = new Promise(resolve => { resolveCheck = resolve; });
  const elements = await page({ check: input => input === 'bad' ? Promise.reject('Invalid address') : checked });
  const input = elements['portal-url'];
  const status = elements['portal-status'];

  input.value = 'bad';
  await elements['portal-url-form'].dispatch('submit');
  assert.equal(status.dataset.state, 'error');
  assert.equal(status.textContent, 'Invalid address');
  assert.equal(elements['portal-confirm'].hidden, true);

  input.value = 'old.example.com';
  const pending = elements['portal-url-form'].dispatch('submit');
  input.value = 'new.example.com';
  input.dispatch('input');
  resolveCheck({ origin: 'https://old.example.com', outcome: { kind: 'reachable' } });
  await pending;
  assert.equal(status.textContent, '');
  assert.equal(elements['portal-confirm'].hidden, true);
});

// @spec CLIENT-012 CLIENT-028
it('presents an unsuccessful connection check as an error', async () => {
  const elements = await page({
    check: async () => ({ origin: 'https://portal.example.com', outcome: { kind: 'network' } }),
  });
  await elements['portal-url-form'].dispatch('submit');
  assert.equal(elements['portal-status'].dataset.state, 'error');
  assert.match(elements['portal-status'].textContent, /network connection/);
  assert.equal(elements['portal-confirm'].hidden, true);
});

// @spec CLIENT-013 CLIENT-028
it('keeps saving explicit and labels the saved address as unverified', async () => {
  const savedOrigins = [];
  const elements = await page({
    check: async () => ({ origin: 'https://portal.example.com', outcome: { kind: 'reachable' } }),
    save: async origin => { savedOrigins.push(origin); return origin; },
  });
  elements['portal-url'].value = 'portal.example.com';
  await elements['portal-url-form'].dispatch('submit');
  assert.deepEqual(savedOrigins, []);

  await elements['portal-save'].dispatch('click');
  assert.deepEqual(savedOrigins, ['https://portal.example.com']);
  assert.equal(elements['portal-confirm'].hidden, true);
  assert.equal(elements['portal-saved'].hidden, false);
  assert.match(elements['portal-saved'].textContent, /^Saved Digital Fortress URL:/);
  assert.match(elements['portal-saved'].textContent, /not signed in or verified/);
});

// @spec CLIENT-013 CLIENT-026
it('confirms and saves the discovered origin, not the entered alias', async () => {
  const savedOrigins = [];
  const elements = await page({
    check: async () => ({
      origin: 'https://portal.example.com',
      outcome: { kind: 'signInRequired' },
    }),
    save: async origin => { savedOrigins.push(origin); return origin; },
  });
  elements['portal-url'].value = 'https://alias.example.com';
  await elements['portal-url-form'].dispatch('submit');
  assert.match(elements['portal-status'].textContent, /https:\/\/portal\.example\.com/);
  assert.equal(elements['portal-confirm'].hidden, false);
  await elements['portal-save'].dispatch('click');
  assert.deepEqual(savedOrigins, ['https://portal.example.com']);
});

// @spec CLIENT-024 CLIENT-025 CLIENT-026 CLIENT-028
it('discovery errors are visible and cannot be confirmed', async () => {
  for (const [outcome, message] of [
    [{ kind: 'discoveryInvalid' }, /invalid/],
    [{ kind: 'discoveryTooLarge' }, /8 KiB/],
    [{ kind: 'discoveryResponse', status: 403 }, /HTTP 403/],
  ]) {
    const elements = await page({
      check: async () => ({ origin: 'https://alias.example.com', outcome }),
    });
    await elements['portal-url-form'].dispatch('submit');
    assert.equal(elements['portal-status'].dataset.state, 'error');
    assert.match(elements['portal-status'].textContent, message);
    assert.equal(elements['portal-confirm'].hidden, true);
  }
});

// @spec CLIENT-030 CLIENT-033
it('shows the app version and the saved Portal website version at startup', async () => {
  const elements = await page({ check: async () => null, savedVersion: async () => '1.4.0' });
  assert.equal(elements['app-version'].textContent, 'App 1.2.3');
  assert.equal(elements['website-version'].hidden, false);
  assert.equal(elements['website-version'].textContent, 'Website 1.4.0');
});

// @spec CLIENT-033
it('omits the website version silently when the startup probe fails', async () => {
  const elements = await page({ check: async () => null, savedVersion: async () => { throw new Error('offline'); } });
  assert.equal(elements['website-version'].hidden, true);
  assert.equal(elements['website-version'].textContent, '');
  assert.equal(elements['portal-status'].textContent, '');
  assert.equal(elements['portal-status'].dataset.state, undefined);
});

// @spec CLIENT-032
it('shows the checked website version and clears it on edit or a failed check', async () => {
  let outcome = { kind: 'signInRequired' };
  const elements = await page({
    check: async () => ({ origin: 'https://portal.example.com', outcome, portalVersion: '1.5.0' }),
    savedVersion: async () => '1.4.0',
  });
  const website = elements['website-version'];

  await elements['portal-url-form'].dispatch('submit');
  assert.equal(website.hidden, false);
  assert.equal(website.textContent, 'Website 1.5.0');

  elements['portal-url'].dispatch('input');
  assert.equal(website.hidden, true);
  assert.equal(website.textContent, '');

  await elements['portal-url-form'].dispatch('submit');
  assert.equal(website.textContent, 'Website 1.5.0');
  outcome = { kind: 'network' };
  await elements['portal-url-form'].dispatch('submit');
  assert.equal(website.hidden, true);
});

// @spec CLIENT-032
it('does not show a website version when a successful check returns none', async () => {
  const elements = await page({
    check: async () => ({ origin: 'https://portal.example.com', outcome: { kind: 'reachable' }, portalVersion: null }),
    savedVersion: async () => '1.4.0',
  });
  await elements['portal-url-form'].dispatch('submit');
  assert.equal(elements['website-version'].hidden, true);
});
