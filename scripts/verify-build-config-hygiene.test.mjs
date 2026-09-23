import assert from "node:assert/strict";
import test from "node:test";
import {
  inspectBuildConfig,
  isBuildConfigPath,
  MAX_BUILD_CONFIG_BYTES,
  MAX_BUILD_CONFIG_LINE_LENGTH,
} from "./verify-build-config-hygiene.mjs";

test("recognizes common JavaScript build configuration files", () => {
  assert.equal(isBuildConfigPath("frontend/vite.config.js"), true);
  assert.equal(isBuildConfigPath("frontend/postcss.config.mjs"), true);
  assert.equal(isBuildConfigPath("frontend/vitest.config.js"), true);
  assert.equal(isBuildConfigPath("frontend/src/app.js"), false);
});

test("accepts a small, readable Vite config", () => {
  const findings = inspectBuildConfig(
    "frontend/vite.config.js",
    "import { defineConfig } from 'vite';\nexport default defineConfig({});\n",
  );

  assert.deepEqual(findings, []);
});

test("rejects the known loader marker in a build config", () => {
  const findings = inspectBuildConfig(
    "frontend/postcss.config.mjs",
    "export default {};\nglobal.i = 'A8-7020-2';\n",
  );

  assert.ok(findings.some((finding) => finding.includes("A8-7020-2")));
});

test("rejects an obfuscated single-line build config", () => {
  const findings = inspectBuildConfig(
    "frontend/vite.config.js",
    `export default {};\n${"x".repeat(MAX_BUILD_CONFIG_LINE_LENGTH + 1)}\n`,
  );

  assert.ok(findings.some((finding) => finding.includes("line 2")));
});

test("rejects an unexpectedly large build config", () => {
  const findings = inspectBuildConfig(
    "frontend/vite.config.js",
    "x".repeat(MAX_BUILD_CONFIG_BYTES + 1),
  );

  assert.ok(findings.some((finding) => finding.includes("maximum is")));
});

test("does not apply build-config heuristics to ordinary source files", () => {
  const findings = inspectBuildConfig("frontend/src/app.js", "A8-7020-2\n".repeat(2_000));

  assert.deepEqual(findings, []);
});
