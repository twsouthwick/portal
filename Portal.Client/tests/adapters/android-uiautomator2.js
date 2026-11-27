import { spawn } from 'node:child_process';
import { writeFile } from 'node:fs/promises';
import net from 'node:net';
import { Builder, By, error } from 'selenium-webdriver';

const packageName = 'app.swick.portal.client';

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
  let serviceError;
  let output = '';

  return {
    async launch() {
      const port = await unusedPort();
      service = spawn('appium', ['--address', '127.0.0.1', '--port', String(port)]);
      service.stdout.on('data', data => { output += data; });
      service.stderr.on('data', data => { output += data; });
      service.on('error', error => { serviceError = error; });

      const deadline = Date.now() + 30000;
      while (true) {
        if (serviceError || service.exitCode !== null || service.signalCode !== null) {
          throw new Error(`Appium failed to start: ${serviceError ?? service.exitCode ?? service.signalCode}`);
        }
        try {
          const response = await fetch(`http://127.0.0.1:${port}/status`, { signal: AbortSignal.timeout(1000) });
          if (response.ok) break;
        } catch {
          // Wait for the driver to start listening.
        }
        if (Date.now() >= deadline) throw new Error('Timed out starting Appium.');
        await new Promise(resolve => setTimeout(resolve, 200));
      }

      driver = await new Builder()
        .usingServer(`http://127.0.0.1:${port}`)
        .withCapabilities({
          browserName: '',
          platformName: 'Android',
          'appium:automationName': 'UiAutomator2',
          'appium:deviceName': 'Android Emulator',
          'appium:appPackage': packageName,
          'appium:appActivity': '.MainActivity',
          'appium:noReset': true,
        })
        .build();
    },

    async isVisible({ role, text }) {
      try {
        return await driver.wait(async () => {
          const webviews = await driver.findElements(By.className('android.webkit.WebView'));
          for (const webview of webviews) {
            const elements = await webview.findElements(By.xpath('.//*'));
            for (const element of elements) {
              const label = (await element.getText()).trim();
              const description = (await element.getAttribute('content-desc') ?? '').trim();
              if ((label === text || description === text) && await element.isDisplayed() &&
                (!role || (role === 'heading' && await element.getAttribute('heading') === 'true'))) return true;
            }
          }
          return false;
        }, 10000);
      } catch (failure) {
        if (failure instanceof error.TimeoutError) return false;
        throw failure;
      }
    },

    async captureScreenshot(file) {
      if (!driver) throw new Error('Cannot capture a screenshot: the Android driver did not start.');
      const screenshot = await driver.takeScreenshot();
      if (!screenshot) throw new Error('The WebDriver returned an empty screenshot.');
      await writeFile(file, screenshot, 'base64');
    },

    async diagnostics() {
      return output ? `Appium output:\n${output}` : '';
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
