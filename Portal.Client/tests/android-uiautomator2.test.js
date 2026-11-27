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

it('checks native visibility and heading attributes for matching WebView content', async () => {
  const query = { role: 'heading', text: 'Portal' };
  assert.equal(await isVisible(driverFor({ displayed: 'true', heading: 'true' }, 'Portal'), query), true);
  assert.equal(await isVisible(driverFor({ displayed: 'false', heading: 'true' }, 'Portal'), query), false);
  assert.equal(await isVisible(driverFor({ displayed: 'true', heading: 'false' }, 'Portal'), query), false);
  assert.equal(await isVisible(driverFor({ displayed: 'true', heading: 'true' }, 'Other'), query), false);
  assert.equal(await isVisible(driverFor({ displayed: 'true', 'content-desc': 'Native client shell' }), { text: 'Native client shell' }), true);
});

it('records matching native attributes when a heading assertion fails', async () => {
  const observations = [];
  assert.equal(await isVisible(
    driverFor({ displayed: 'true', heading: 'false' }, 'Portal'),
    { role: 'heading', text: 'Portal' },
    observation => observations.push(observation),
  ), false);
  assert.deepEqual(observations, ['Matched requested text "Portal": displayed=true, heading=false']);
});
