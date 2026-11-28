import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { after, before, describe, it } from 'node:test';

const clientDir = fileURLToPath(new URL('../', import.meta.url));
const artifactsDir = path.resolve(
  clientDir,
  process.env.PORTAL_CLIENT_ARTIFACTS ?? 'artifacts',
);
const adapterModule = await import(new URL(
  process.env.PORTAL_CLIENT_TEST_ADAPTER ?? './adapters/linux-tauri.js',
  import.meta.url,
));
const adapter = await adapterModule.createAdapter();
let startupError;
let screenshotAttempted = false;
let screenshotError;
let diagnosticsWritten = false;
let cleanupAttempted = false;

async function writeDiagnostics(error) {
  const diagnostics = await adapter.diagnostics();
  await writeFile(
    path.join(artifactsDir, 'diagnostics.log'),
    [error?.stack ?? error?.message ?? error, diagnostics].filter(Boolean).join('\n\n'),
  );
}

// @spec CLIENT-001 CLIENT-003
describe('Portal native client', { timeout: 180000 }, () => {
  before(async () => {
    await mkdir(artifactsDir, { recursive: true });
    try {
      await adapter.launch();
    } catch (error) {
      startupError = error;
      await writeDiagnostics(error);
      throw error;
    }
  });

  after(async () => {
    if (!screenshotAttempted) {
      try {
        await adapter.captureScreenshot(path.join(artifactsDir, 'portal-client.png'));
      } catch (error) {
        screenshotError = error;
      }
    }
    if (!diagnosticsWritten) {
      try {
        await writeDiagnostics([screenshotError, startupError].filter(Boolean).join('\n\n'));
      } catch {
        // A setup failure already fails the suite; the upload step reports missing evidence.
      }
    }
    if (!cleanupAttempted) {
      try {
        await adapter.cleanup();
      } catch {
        // A setup failure already fails the suite; still finish teardown best-effort.
      }
    }
  });

  it('shows the visible onboarding heading', async () => {
    assert.equal(await adapter.isVisible({ role: 'heading', text: 'Add your Digital Fortress' }), true);
  });

  it('explains how to get started', async () => {
    assert.equal(await adapter.isVisible({ text: 'Enter your Digital Fortress web address to get started.' }), true);
  });

  it('captures a screenshot of the running client', async () => {
    screenshotAttempted = true;
    try {
      await adapter.captureScreenshot(path.join(artifactsDir, 'portal-client.png'));
    } catch (error) {
      screenshotError = error;
      throw error;
    }
  });

  it('writes driver diagnostics', async () => {
    await writeDiagnostics(screenshotError);
    diagnosticsWritten = true;
  });

  it('cleans up the client driver', async () => {
    cleanupAttempted = true;
    await adapter.cleanup();
  });
});
