import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const adb = promisify(execFile);

export async function connectWebView(packageName, { runAdb = adb, fetchImpl = fetch, Socket = WebSocket, discoveryTimeout = 15000 } = {}) {
  const { stdout } = await runAdb('adb', ['shell', 'pidof', packageName]);
  const pid = stdout.trim();
  if (!/^\d+$/.test(pid)) throw new Error(`Expected one running process for ${packageName}, got ${JSON.stringify(pid)}.`);

  const forwarded = await runAdb('adb', ['forward', 'tcp:0', `localabstract:webview_devtools_remote_${pid}`]);
  const port = forwarded.stdout.trim();
  if (!/^\d+$/.test(port)) throw new Error(`Invalid adb forwarded port: ${JSON.stringify(port)}`);
  const removeForward = () => runAdb('adb', ['forward', '--remove', `tcp:${port}`]);
  let socket;

  try {
    const deadline = Date.now() + discoveryTimeout;
    let pages = [];
    let discoveryError;
    do {
      try {
        const response = await fetchImpl(`http://127.0.0.1:${port}/json/list`, { signal: AbortSignal.timeout(1000) });
        if (!response.ok) throw new Error(`WebView target discovery failed: ${response.status}`);
        const targets = await response.json();
        pages = targets.filter(target => {
          try {
            const url = new URL(target.url);
            return target.type === 'page' && url.hostname === 'tauri.localhost' &&
              ['http:', 'https:'].includes(url.protocol);
          } catch {
            return false;
          }
        });
        discoveryError = undefined;
      } catch (error) {
        discoveryError = error;
      }
      if (pages.length || Date.now() >= deadline) break;
      await new Promise(resolve => setTimeout(resolve, 200));
    } while (true);
    if (discoveryError) throw discoveryError;
    if (pages.length !== 1) throw new Error(`Expected one Tauri WebView page, found ${pages.length}.`);
    const webSocketPath = new URL(pages[0].webSocketDebuggerUrl).pathname;
    socket = new Socket(`ws://127.0.0.1:${port}${webSocketPath}`);
    await new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('Timed out connecting to the Tauri WebView.')), 10000);
      socket.addEventListener('open', () => { clearTimeout(timer); resolve(); }, { once: true });
      socket.addEventListener('error', () => { clearTimeout(timer); reject(new Error('Failed to connect to the Tauri WebView.')); }, { once: true });
    });

    let nextId = 0;
    return {
      async evaluate(expression) {
        const id = ++nextId;
        return new Promise((resolve, reject) => {
          const finish = (error, value) => {
            clearTimeout(timer);
            socket.removeEventListener('message', onMessage);
            socket.removeEventListener('close', onClose);
            error ? reject(error) : resolve(value);
          };
          const onMessage = event => {
            let message;
            try { message = JSON.parse(event.data); } catch (error) { finish(error); return; }
            if (message.id !== id) return;
            if (message.error || message.result?.exceptionDetails) {
              finish(new Error(`WebView evaluation failed: ${JSON.stringify(message.error ?? message.result.exceptionDetails)}`));
            } else {
              finish(null, message.result?.result?.value);
            }
          };
          const onClose = () => finish(new Error('WebView connection closed.'));
          const timer = setTimeout(() => finish(new Error('Timed out evaluating the WebView.')), 10000);
          socket.addEventListener('message', onMessage);
          socket.addEventListener('close', onClose, { once: true });
          try {
            socket.send(JSON.stringify({ id, method: 'Runtime.evaluate', params: { expression, returnByValue: true } }));
          } catch (error) {
            finish(error);
          }
        });
      },
      async cleanup() {
        socket.close();
        await removeForward();
      },
    };
  } catch (error) {
    if (socket) socket.close();
    await removeForward();
    throw error;
  }
}

export function headingExpression(text) {
  return `(() => {
    const expected = ${JSON.stringify(text)};
    return Array.from(document.querySelectorAll('main h1')).some(element => {
      const rect = element.getBoundingClientRect();
      if (element.innerText.trim() !== expected ||
          !element.checkVisibility({ checkOpacity: true, checkVisibilityCSS: true })) return false;
      let left = Math.max(0, rect.left), top = Math.max(0, rect.top);
      let right = Math.min(innerWidth, rect.right), bottom = Math.min(innerHeight, rect.bottom);
      for (let parent = element.parentElement; parent; parent = parent.parentElement) {
        const style = getComputedStyle(parent);
        const bounds = parent.getBoundingClientRect();
        if (['hidden', 'clip', 'scroll', 'auto'].includes(style.overflowX)) {
          left = Math.max(left, bounds.left + parent.clientLeft);
          right = Math.min(right, bounds.left + parent.clientLeft + parent.clientWidth);
        }
        if (['hidden', 'clip', 'scroll', 'auto'].includes(style.overflowY)) {
          top = Math.max(top, bounds.top + parent.clientTop);
          bottom = Math.min(bottom, bounds.top + parent.clientTop + parent.clientHeight);
        }
      }
      return right > left && bottom > top;
    });
  })()`;
}
