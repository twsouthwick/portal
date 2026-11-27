import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export function configureAndroidGradleSigning(source) {
  const buildTypes = [...source.matchAll(/^([ \t]*)buildTypes\s*\{/gm)];
  const releaseBuildTypes = [...source.matchAll(/^([ \t]*)getByName\("release"\)\s*\{/gm)];
  if (buildTypes.length !== 1 || releaseBuildTypes.length !== 1) {
    throw new Error('Expected exactly one Android buildTypes block with one release build type');
  }
  if (source.includes('portalRelease') || source.includes('keystore.properties')) {
    throw new Error('Android signing configuration is already present');
  }

  const buildIndent = buildTypes[0][1];
  const releaseIndent = releaseBuildTypes[0][1];
  const signing = [
    `${buildIndent}signingConfigs {`,
    `${buildIndent}    create("portalRelease") {`,
    `${buildIndent}        val keystorePropertiesFile = rootProject.file("keystore.properties")`,
    `${buildIndent}        val keystoreProperties = Properties()`,
    `${buildIndent}        if (!keystorePropertiesFile.exists()) {`,
    `${buildIndent}            throw GradleException("Missing Android release keystore properties")`,
    `${buildIndent}        }`,
    `${buildIndent}        keystoreProperties.load(FileInputStream(keystorePropertiesFile))`,
    `${buildIndent}        keyAlias = keystoreProperties["keyAlias"] as String`,
    `${buildIndent}        keyPassword = keystoreProperties["password"] as String`,
    `${buildIndent}        storeFile = file(keystoreProperties["storeFile"] as String)`,
    `${buildIndent}        storePassword = keystoreProperties["password"] as String`,
    `${buildIndent}    }`,
    `${buildIndent}}`,
    '',
  ].join('\n');

  let configured = source.replace(buildTypes[0][0], `${signing}${buildTypes[0][0]}`);
  configured = configured.replace(
    releaseBuildTypes[0][0],
    `${releaseIndent}getByName("release") {\n${releaseIndent}    signingConfig = signingConfigs.getByName("portalRelease")`,
  );
  return `import java.io.FileInputStream\nimport java.util.Properties\n\n${configured}`;
}

async function main() {
  const gradlePath = resolve(process.argv[2] ?? '');
  if (!process.argv[2]) throw new Error('Usage: configure-android-signing.mjs <build.gradle.kts>');
  const source = await readFile(gradlePath, 'utf8');
  await writeFile(gradlePath, configureAndroidGradleSigning(source));
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  await main();
}
