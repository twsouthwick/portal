import { spawn } from 'node:child_process';
import { access, writeFile } from 'node:fs/promises';
import net from 'node:net';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { Builder, By, error } from 'selenium-webdriver';

const clientDir = fileURLToPath(new URL('../../', import.meta.url));
const executable = process.platform === 'win32' ? 'portalclient.exe' : 'portalclient';

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

export async function createAdapter() {
  let driver;
  let service;
  let output = '';
  let serviceError;

  return {
    async launch() {
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
        if (serviceError || service.exitCode !== null || service.signalCode !== null) {
          throw new Error(`tauri-driver failed to start: ${serviceError ?? service.exitCode ?? service.signalCode}`);
        }
        try {
          const response = await fetch(`http://127.0.0.1:${port}/status`, { signal: AbortSignal.timeout(1000) });
          if (response.ok) break;
        } catch {
          // Wait for the driver to start listening.
        }
        if (Date.now() >= deadline) throw new Error('Timed out starting tauri-driver.');
        await new Promise(resolve => setTimeout(resolve, 200));
      }

      driver = await new Builder()
        .usingServer(`http://127.0.0.1:${port}`)
        .withCapabilities({ browserName: 'wry', 'tauri:options': { application } })
        .build();
    },

    async isVisible({ role, text }) {
      const selector = role === 'heading' ? 'main h1' : 'main p';
      try {
        return await driver.wait(async () => {
          const elements = await driver.findElements(By.css(selector));
          for (const element of elements) {
            if (await element.isDisplayed() && (await element.getText()).trim() === text) return true;
          }
          return false;
        }, 10000);
      } catch (failure) {
        if (failure instanceof error.TimeoutError) return false;
        throw failure;
      }
    },

    async captureScreenshot(file) {
      if (!driver) throw new Error('Cannot capture a screenshot: the native WebView did not start.');
      const screenshot = await driver.takeScreenshot();
      if (!screenshot) throw new Error('The WebDriver returned an empty screenshot.');
      await writeFile(file, screenshot, 'base64');
    },

    async diagnostics() {
      return output ? `tauri-driver output:\n${output}` : '';
    },

    async cleanup() {
      try {
        if (driver) await driver.quit();
      } finally {
        if (service && service.exitCode === null && service.signalCode === null && !serviceError) {
          const exited = new Promise(resolve => service.once('exit', resolve));
          service.kill();
          await exited;
        }
      }
    },
  };
}
