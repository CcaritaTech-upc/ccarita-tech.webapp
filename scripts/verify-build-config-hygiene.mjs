import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { resolve, basename } from "node:path";
import { fileURLToPath } from "node:url";

export const MAX_BUILD_CONFIG_BYTES = 16 * 1024;
export const MAX_BUILD_CONFIG_LINE_LENGTH = 1_000;

const buildConfigName = /^(?:vite|postcss|webpack|rollup|next|nuxt|astro|svelte|babel|tailwind|playwright|vitest|eslint)\.config\.(?:[cm]?[jt]s|[jt]sx)$/i;
const knownLoaderMarkers = ["A8-7020-2", "_0x10df86", "Payload-B6"];

export function isBuildConfigPath(filePath) {
  return buildConfigName.test(basename(filePath.replaceAll("\\", "/")));
}

export function inspectBuildConfig(filePath, source) {
  if (!isBuildConfigPath(filePath)) return [];

  const findings = [];
  const size = Buffer.byteLength(source, "utf8");
  if (size > MAX_BUILD_CONFIG_BYTES) {
    findings.push(`file is ${size} bytes; maximum is ${MAX_BUILD_CONFIG_BYTES}`);
  }

  for (const [index, line] of source.split(/\r?\n/).entries()) {
    if (line.length > MAX_BUILD_CONFIG_LINE_LENGTH) {
      findings.push(`line ${index + 1} is ${line.length} characters; maximum is ${MAX_BUILD_CONFIG_LINE_LENGTH}`);
    }
  }

  for (const marker of knownLoaderMarkers) {
    if (source.includes(marker)) {
      findings.push(`contains a known suspicious loader marker: ${marker}`);
    }
  }

  return findings;
}

function main() {
  const root = resolve(fileURLToPath(new URL("..", import.meta.url)));
  let trackedFiles;

  try {
    trackedFiles = execFileSync("git", ["ls-files", "-z"], { cwd: root, encoding: "utf8" })
      .split("\0")
      .filter(Boolean)
      .filter(isBuildConfigPath);
  } catch (error) {
    console.error(`Build configuration scan could not list tracked files: ${error.message}`);
    process.exitCode = 1;
    return;
  }

  const failures = [];
  for (const relativePath of trackedFiles) {
    try {
      const source = readFileSync(resolve(root, relativePath), "utf8");
      for (const finding of inspectBuildConfig(relativePath, source)) {
        failures.push(`${relativePath}: ${finding}`);
      }
    } catch (error) {
      failures.push(`${relativePath}: could not read tracked build config (${error.message})`);
    }
  }

  if (failures.length > 0) {
    console.error("Build configuration hygiene check failed:");
    for (const failure of failures) console.error(`- ${failure}`);
    process.exitCode = 1;
    return;
  }

  console.log(`Build configuration hygiene verified (${trackedFiles.length} files).`);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main();
}
