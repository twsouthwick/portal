import assert from 'node:assert/strict';
import { it } from 'node:test';
import { runInNewContext } from 'node:vm';
import { connectWebView, headingExpression } from './adapters/android-webview.js';

it('requires the exact visible heading in the WebView DOM', () => {
  const heading = {
    innerText: 'Portal',
    checkVisibility: () => true,
    getBoundingClientRect: () => ({ width: 30, height: 20, top: 0, left: 0, bottom: 20, right: 30 }),
  };
  const evaluate = elements => runInNewContext(headingExpression('Portal'), {
    document: { querySelectorAll: selector => selector === 'main h1' ? elements : [] },
    innerHeight: 100,
    innerWidth: 100,
    getComputedStyle: () => ({ overflowX: 'hidden', overflowY: 'hidden' }),
  });
  assert.equal(evaluate([heading]), true);
  assert.equal(evaluate([{ ...heading, innerText: 'Other' }]), false);
  assert.equal(evaluate([{ ...heading, checkVisibility: () => false }]), false);
  assert.equal(evaluate([{ ...heading, getBoundingClientRect: () => ({ width: 30, height: 20, top: 100, left: 0, bottom: 120, right: 30 }) }]), false);
  assert.equal(evaluate([{ ...heading, parentElement: {
    getBoundingClientRect: () => ({ left: 50, top: 50 }),
    clientLeft: 0, clientTop: 0, clientWidth: 20, clientHeight: 20,
  } }]), false);
  assert.equal(evaluate([]), false);
});

it('connects only to the installed app WebView and releases the adb forward', async () => {
  const commands = [];
  const runAdb = async (command, args) => {
    commands.push([command, ...args]);
    return { stdout: args[0] === 'shell' ? '123\n' : args[1] === 'tcp:0' ? '4321\n' : '' };
  };
  class Socket extends EventTarget {
    constructor(url) {
      super();
      assert.equal(url, 'ws://127.0.0.1:4321/devtools/page/123');
      queueMicrotask(() => this.dispatchEvent(new Event('open')));
    }
    send(data) {
      const request = JSON.parse(data);
      assert.equal(request.method, 'Runtime.evaluate');
      queueMicrotask(() => this.dispatchEvent(new MessageEvent('message', {
        data: JSON.stringify({ id: request.id, result: { result: { value: true } } }),
      })));
    }
    close() { this.closed = true; }
  }
  let attempts = 0;
  const fetchImpl = async url => {
    assert.equal(url, 'http://127.0.0.1:4321/json/list');
    return { ok: true, json: async () => ++attempts === 1 ? [] : [{
      type: 'page', url: 'http://tauri.localhost/', webSocketDebuggerUrl: 'ws://localhost/devtools/page/123',
    }] };
  };
  const webview = await connectWebView('app.swick.portal.client', { runAdb, fetchImpl, Socket });
  assert.equal(attempts, 2);
  assert.equal(await webview.evaluate('true'), true);
  await webview.cleanup();
  assert.deepEqual(commands, [
    ['adb', 'shell', 'pidof', 'app.swick.portal.client'],
    ['adb', 'forward', 'tcp:0', 'localabstract:webview_devtools_remote_123'],
    ['adb', 'forward', '--remove', 'tcp:4321'],
  ]);
});

it('evaluates matching content across multiple Tauri WebView pages', async () => {
  const commands = [];
  const runAdb = async (command, args) => {
    commands.push([command, ...args]);
    return { stdout: args[0] === 'shell' ? '123\n' : args[1] === 'tcp:0' ? '4321\n' : '' };
  };
  const opened = [];
  class Socket extends EventTarget {
    constructor(url) {
      super();
      this.matches = new URL(url).pathname.endsWith('/active');
      opened.push(this);
      queueMicrotask(() => this.dispatchEvent(new Event('open')));
    }
    send(data) {
      const request = JSON.parse(data);
      queueMicrotask(() => this.dispatchEvent(new MessageEvent('message', {
        data: JSON.stringify({ id: request.id, result: { result: { value: this.matches } } }),
      })));
    }
    close() { this.closed = true; }
  }
  const fetchImpl = async () => ({
    ok: true,
    json: async () => [
      { type: 'page', url: 'http://tauri.localhost/first', webSocketDebuggerUrl: 'ws://localhost/devtools/page/first' },
      { type: 'page', url: 'http://tauri.localhost/active', webSocketDebuggerUrl: 'ws://localhost/devtools/page/active' },
    ],
  });
  const webview = await connectWebView('app.swick.portal.client', { runAdb, fetchImpl, Socket });
  assert.equal(opened.length, 2);
  assert.equal(await webview.evaluate('true'), true);
  await webview.cleanup();
  assert.ok(opened.every(socket => socket.closed));
  assert.deepEqual(commands.at(-1), ['adb', 'forward', '--remove', 'tcp:4321']);
});

it('fails closed and removes the forward for unrelated WebView targets', async () => {
  const commands = [];
  const runAdb = async (command, args) => {
    commands.push(args);
    return { stdout: args[0] === 'shell' ? '123' : '4321' };
  };
  const fetchImpl = async () => ({ ok: true, json: async () => [{ type: 'page', url: 'https://example.com' }] });
  await assert.rejects(connectWebView('app.swick.portal.client', { runAdb, fetchImpl, discoveryTimeout: 0 }), /Expected at least one Tauri WebView page/);
  assert.deepEqual(commands.at(-1), ['forward', '--remove', 'tcp:4321']);
});
