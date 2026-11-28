import { test } from 'node:test';
import assert from 'node:assert/strict';
import { configureAndroidGradleSigning, escapeJavaPropertiesValue } from './configure-android-signing.mjs';

const generatedGradle = `plugins {
    id("com.android.application")
}

android {
    buildTypes {
        getByName("release") {
            isMinifyEnabled = false
        }
    }
}
`;

// @spec DELIVERY-012 DELIVERY-019 CLIENT-006
test('Android generated Gradle config loads the distribution keystore for release APKs', () => {
  const configured = configureAndroidGradleSigning(generatedGradle);
  assert.doesNotMatch(configured, /^import /m);
  assert.match(configured, /val keystoreProperties = java\.util\.Properties\(\)/);
  assert.match(configured, /keystoreProperties\.load\(java\.io\.FileInputStream\(keystorePropertiesFile\)\)/);
  assert.match(configured, /keystore\.properties/);
  assert.match(configured, /signingConfig = signingConfigs\.getByName\("portalRelease"\)/);
});

// @spec DELIVERY-019
test('Android signing preserves Java imports already present in generated Gradle scripts', () => {
  for (const imports of [
    'import java.util.Properties\n\n',
    'import java.io.FileInputStream\nimport java.util.Properties\n\n',
  ]) {
    const source = imports + generatedGradle;
    const configured = configureAndroidGradleSigning(source);
    assert.equal(configured.slice(0, configured.indexOf('plugins {')), imports);
    assert.match(configured, /val keystoreProperties = java\.util\.Properties\(\)/);
    assert.match(configured, /keystoreProperties\.load\(java\.io\.FileInputStream\(keystorePropertiesFile\)\)/);
    assert.match(configured, /signingConfig = signingConfigs\.getByName\("portalRelease"\)/);
  }
});

// @spec CLIENT-006
test('Android signing secrets are escaped for Java Properties values', () => {
  assert.equal(escapeJavaPropertiesValue(' \\tfoo:=#! café\n'), '\\ \\\\tfoo\\:\\=\\#\\! caf\\u00e9\\n');
});

// @spec CLIENT-006
test('Android signing configuration rejects unexpected generated Gradle structure', () => {
  assert.throws(() => configureAndroidGradleSigning('android { }'), /Expected exactly one/);
});
