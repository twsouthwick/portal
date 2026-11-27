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

// @spec CLIENT-006
test('Android generated Gradle config loads the distribution keystore for release APKs', () => {
  const configured = configureAndroidGradleSigning(generatedGradle);
  assert.match(configured, /import java\.io\.FileInputStream/);
  assert.match(configured, /import java\.util\.Properties/);
  assert.match(configured, /keystore\.properties/);
  assert.match(configured, /signingConfig = signingConfigs\.getByName\("portalRelease"\)/);
});

// @spec CLIENT-006
test('Android signing secrets are escaped for Java Properties values', () => {
  assert.equal(escapeJavaPropertiesValue(' \\tfoo:=#! café\n'), '\\ \\\\tfoo\\:\\=\\#\\! caf\\u00e9\\n');
});

// @spec CLIENT-006
test('Android signing configuration rejects unexpected generated Gradle structure', () => {
  assert.throws(() => configureAndroidGradleSigning('android { }'), /Expected exactly one/);
});
