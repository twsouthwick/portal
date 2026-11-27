import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const workflow = await readFile(new URL('../workflows/native-release.yaml', import.meta.url), 'utf8');

// @spec DELIVERY-012 CLIENT-006
test('Android release builds one signed APK for arm64-v8a and x86_64', () => {
  assert.match(workflow, /android build --ci --apk --target aarch64 --target x86_64/);
  assert.match(workflow, /ANDROID_KEYSTORE_BASE64/);
  assert.match(workflow, /ANDROID_KEYSTORE_PASSWORD/);
  assert.match(workflow, /ANDROID_KEY_ALIAS/);
  assert.match(workflow, /apksigner.*verify/);
  assert.match(workflow, /arm64-v8a/);
  assert.match(workflow, /native_code[\s\S]*arm64-v8a[\s\S]*x86_64/);
  assert.match(workflow, /configure-android-signing\.mjs/);
  assert.doesNotMatch(workflow, /smoke\.keystore/);
});

// @spec CLIENT-007
test('native artifacts receive MinVer display versions and monotonic Android version codes', () => {
  assert.match(workflow, /MinVerVersion/);
  assert.match(workflow, /versionCode/);
  assert.match(workflow, /GITHUB_RUN_NUMBER/);
  assert.match(workflow, /version=%s/);
});
