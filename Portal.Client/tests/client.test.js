import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { access } from 'node:fs/promises';
import net from 'node:net';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { after, before, describe, it } from 'node:test';
import { Builder, By, until } from 'selenium-webdriver';

const clientDir = fileURLToPath(new URL('../', import.meta.url));
const executable = {
  linux: 'portalclient',
  win32: 'portalclient.exe',
}[process.platform];

async function unusedPort() {
  const server = net.createServer();
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });
  const port = server.address().port;
  await new Promise((resolve, reject) => server.close(error => error ? reject(error) : resolve()));
  return port;
}

describe('Portal native client', { timeout: 60000 }, () => {
  let driver;
  let service;
  let output = '';
  let serviceError;

  before(async () => {
    assert.ok(executable, `Tauri WebDriver is not supported on ${process.platform}`);
    const application = path.join(clientDir, 'src-tauri', 'target', 'debug', executable);
    await access(application);
    const port = await unusedPort();
    let nativePort = await unusedPort();
    while (nativePort === port) nativePort = await unusedPort();
    service = spawn('tauri-driver', ['--port', String(port), '--native-port', String(nativePort)]);
    service.stdout.on('data', data => { output += data; });
    service.stderr.on('data', data => { output += data; });
    service.on('error', error => { serviceError = error; });

    const deadline = Date.now() + 10000;
    while (true) {
      if (serviceError || service.exitCode !== null) {
        throw new Error(`tauri-driver failed to start: ${serviceError ?? service.exitCode}\n${output}`);
      }
      try {
        const response = await fetch(`http://127.0.0.1:${port}/status`, { signal: AbortSignal.timeout(1000) });
        if (response.ok) break;
      } catch {
        // Wait for the driver to start listening.
      }
      if (Date.now() >= deadline) throw new Error(`Timed out starting tauri-driver:\n${output}`);
      await new Promise(resolve => setTimeout(resolve, 200));
    }

    driver = await new Builder()
      .usingServer(`http://127.0.0.1:${port}`)
      .withCapabilities({ browserName: 'wry', 'tauri:options': { application } })
      .build();
  });

  after(async () => {
    try {
      if (driver) await driver.quit();
    } finally {
      if (service && service.exitCode === null && !serviceError) {
        const exited = new Promise(resolve => service.once('exit', resolve));
        service.kill();
        await exited;
      }
      if (output) console.log(`tauri-driver output:\n${output}`);
    }
  });

  it('shows the Portal heading in the native webview', async () => {
    const heading = await driver.wait(until.elementLocated(By.css('main h1')), 10000);
    assert.equal(await heading.getText(), 'Portal');
  });

  it('shows the client shell placeholder', async () => {
    assert.equal(await driver.findElement(By.css('main p')).getText(), 'Native client shell');
  });
});
