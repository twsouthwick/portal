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
  assert.equal(configured.match(/^import java\.util\.Properties$/gm)?.length, 1);
  assert.ok(configured.startsWith('import java.util.Properties\n'));
  assert.match(configured, /val keystoreProperties = Properties\(\)/);
  assert.match(configured, /keystorePropertiesFile\.inputStream\(\)\.use \{ keystoreProperties\.load\(it\) \}/);
  assert.match(configured, /keystore\.properties/);
  assert.match(configured, /signingConfig = signingConfigs\.getByName\("portalRelease"\)/);
});

// @spec DELIVERY-019
test('Android signing adds no duplicate imports and no fully qualified java references', () => {
  for (const imports of [
    'import java.util.Properties\n\n',
    'import java.io.FileInputStream\nimport java.util.Properties\n\n',
  ]) {
    const configured = configureAndroidGradleSigning(imports + generatedGradle);
    assert.equal(configured.slice(0, configured.indexOf('plugins {')), imports);
    assert.equal(configured.match(/^import java\.util\.Properties$/gm)?.length, 1);
  }
  const configured = configureAndroidGradleSigning(generatedGradle);
  assert.doesNotMatch(configured.slice(configured.indexOf('android {')), /\bjava\./);
});

// @spec CLIENT-006
test('Android signing secrets are escaped for Java Properties values', () => {
  assert.equal(escapeJavaPropertiesValue(' \\tfoo:=#! café\n'), '\\ \\\\tfoo\\:\\=\\#\\! caf\\u00e9\\n');
});

// @spec CLIENT-006
test('Android signing configuration rejects unexpected generated Gradle structure', () => {
  assert.throws(() => configureAndroidGradleSigning('android { }'), /Expected exactly one/);
});
