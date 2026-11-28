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
  assert.match(configured, /^import java\.util\.Properties as PortalSigningProperties$/m);
  assert.match(configured, /val keystoreProperties = PortalSigningProperties\(\)/);
  assert.match(configured, /keystorePropertiesFile\.inputStream\(\)\.use \{ keystoreProperties\.load\(it\) \}/);
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
    assert.ok(configured.includes(imports));
    assert.equal((configured.match(/^import java\.util\.Properties as PortalSigningProperties$/gm) ?? []).length, 1);
    assert.match(configured, /val keystoreProperties = PortalSigningProperties\(\)/);
    assert.match(configured, /keystorePropertiesFile\.inputStream\(\)\.use \{ keystoreProperties\.load\(it\) \}/);
    assert.match(configured, /signingConfig = signingConfigs\.getByName\("portalRelease"\)/);
  }
});

// @spec DELIVERY-019
test('Android signing reuses its existing alias and rejects an incompatible alias', () => {
  const existing = 'import java.util.Properties as PortalSigningProperties\n';
  const configured = configureAndroidGradleSigning(existing + generatedGradle);
  assert.equal((configured.match(/^import java\.util\.Properties as PortalSigningProperties$/gm) ?? []).length, 1);
  assert.throws(
    () => configureAndroidGradleSigning('import example.Other as PortalSigningProperties\n' + generatedGradle),
    /PortalSigningProperties import alias is already in use/,
  );
});

// @spec CLIENT-006
test('Android signing secrets are escaped for Java Properties values', () => {
  assert.equal(escapeJavaPropertiesValue(' \\tfoo:=#! café\n'), '\\ \\\\tfoo\\:\\=\\#\\! caf\\u00e9\\n');
});

// @spec CLIENT-006
test('Android signing configuration rejects unexpected generated Gradle structure', () => {
  assert.throws(() => configureAndroidGradleSigning('android { }'), /Expected exactly one/);
});
