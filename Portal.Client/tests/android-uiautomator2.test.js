import assert from 'node:assert/strict';
import { it } from 'node:test';
import { isVisible } from './adapters/android-uiautomator2.js';

function driverFor(attributes, text = '') {
  const element = {
    async getText() { return text; },
    async getDomAttribute(name) { return attributes[name] ?? null; },
    async isDisplayed() { throw new Error('Native visibility must not execute JavaScript'); },
  };
  return {
    async wait(condition) { return condition(); },
    async findElements() {
      return [{ async findElements() { return [element]; } }];
    },
  };
}

it('checks native visibility for matching WebView content', async () => {
  const query = { text: 'Portal' };
  assert.equal(await isVisible(driverFor({ displayed: 'true' }, 'Portal'), query), true);
  assert.equal(await isVisible(driverFor({ displayed: 'false' }, 'Portal'), query), false);
  assert.equal(await isVisible(driverFor({ displayed: 'true' }, 'Other'), query), false);
  assert.equal(await isVisible(driverFor({ displayed: 'true', 'content-desc': 'Native client shell' }), { text: 'Native client shell' }), true);
  await assert.rejects(isVisible(driverFor({ displayed: 'true' }, 'Portal'), { ...query, role: 'heading' }), /Unsupported native UI role/);
});

it('records matching native visibility when an assertion fails', async () => {
  const observations = [];
  assert.equal(await isVisible(
    driverFor({ displayed: 'false' }, 'Portal'),
    { text: 'Portal' },
    observation => observations.push(observation),
  ), false);
  assert.deepEqual(observations, ['Matched requested text "Portal": displayed=false']);
});
