import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';
import { runInNewContext } from 'node:vm';

const source = await readFile(new URL('../src/main.js', import.meta.url), 'utf8');

function setup(outcome) {
  const ids = [
    'portal-url-form', 'portal-url', 'portal-status',
    'portal-confirm', 'portal-save', 'portal-saved',
  ];
  const elements = Object.fromEntries(ids.map(id => [id, {
    hidden: true,
    textContent: '',
    value: '',
    handlers: {},
    addEventListener(event, handler) {
      this.handlers[event] = handler;
    },
  }]));
  const calls = [];
  const invoke = async (command, args) => {
    calls.push({ command, args });
    if (command === 'check_portal_url') return outcome;
    if (command === 'save_portal_url') return args.origin;
    if (command === 'saved_portal_url') return null;
    throw new Error(`Unexpected command: ${command}`);
  };
  runInNewContext(source, {
    window: { __TAURI__: { core: { invoke } } },
    document: { getElementById: id => elements[id] },
  });
  return { elements, calls };
}

// @spec CLIENT-013 CLIENT-017
test('confirms and saves the discovered origin, not the entered alias', async () => {
  const { elements, calls } = setup({
    origin: 'https://portal.example.com',
    outcome: { kind: 'signInRequired' },
  });
  elements['portal-url'].value = 'https://alias.example.com';

  await elements['portal-url-form'].handlers.submit({ preventDefault() {} });

  assert.match(elements['portal-status'].textContent, /https:\/\/portal\.example\.com/);
  assert.equal(elements['portal-confirm'].hidden, false);
  await elements['portal-save'].handlers.click();
  const save = calls.find(call => call.command === 'save_portal_url');
  assert.equal(save.args.origin, 'https://portal.example.com');
});

// @spec CLIENT-015 CLIENT-016 CLIENT-017
test('discovery errors are visible and cannot be confirmed', async () => {
  for (const [outcome, message] of [
    [{ kind: 'discoveryInvalid' }, /invalid/],
    [{ kind: 'discoveryTooLarge' }, /8 KiB/],
    [{ kind: 'discoveryResponse', status: 403 }, /HTTP 403/],
  ]) {
    const { elements } = setup({ origin: 'https://alias.example.com', outcome });
    await elements['portal-url-form'].handlers.submit({ preventDefault() {} });
    assert.match(elements['portal-status'].textContent, message);
    assert.equal(elements['portal-confirm'].hidden, true);
  }
});
