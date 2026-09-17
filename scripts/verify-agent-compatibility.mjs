import { access, readFile } from "node:fs/promises";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const skillNames = [
  "convergent-testing",
  "convergent-journey-design",
  "convergent-gate-execution",
  "convergent-failure-diagnosis",
];

const failures = [];

async function requireFile(relativePath) {
  try {
    await access(resolve(root, relativePath));
  } catch {
    failures.push(`Missing ${relativePath}`);
  }
}

async function forbidFile(relativePath) {
  try {
    await access(resolve(root, relativePath));
    failures.push(`Remove stale duplicate ${relativePath}`);
  } catch {
    // Expected: canonical skills must not be duplicated in tool-specific trees.
  }
}

async function requireText(relativePath, expected) {
  try {
    const text = await readFile(resolve(root, relativePath), "utf8");
    if (!text.includes(expected)) {
      failures.push(`${relativePath} does not contain ${JSON.stringify(expected)}`);
    }
  } catch {
    failures.push(`Missing ${relativePath}`);
  }
}

for (const name of skillNames) {
  await requireText(`.agents/skills/${name}/SKILL.md`, `name: ${name}`);
  await requireText(
    `.claude/skills/${name}/SKILL.md`,
    `../../../.agents/skills/${name}/SKILL.md`,
  );
  await forbidFile(`.opencode/skills/${name}/SKILL.md`);
}

await requireFile(".agents/skills/convergent-testing/references/gate-protocol.md");
await requireFile(".agents/skills/convergent-testing/references/scenario-schema.md");
await requireText("AGENTS.md", "Mandatory Convergent Testing Gate");
await requireText("AGENTS.md", "docs/delivery-discipline.md");
await requireText("CLAUDE.md", "@AGENTS.md");
await requireText("opencode.json", '".agents/skills"');
await requireText(".agents/rules/convergent-testing.md", "served actors");
await requireText(".agents/agents/convergent-qa.md", "name: convergent-qa");
await requireFile(".opencode/agents/convergent-qa.md");
await requireFile(".opencode/commands/convergent-gate.md");
await requireText("docs/agent-compatibility.md", ".agents/skills/");
await requireText(".agents/skills/convergent-testing/SKILL.md", "actor_coverage");
await requireText(".agents/skills/convergent-gate-execution/SKILL.md", "Every open risk needs an owner");
await requireText(".agents/skills/convergent-testing/references/scenario-schema.md", "roles_covered");
await requireText("convergent-testing/README.md", "Runtime and product records");
await requireText("docs/delivery-discipline.md", "Transversal coverage across roles");

if (failures.length > 0) {
  console.error("Agent compatibility verification failed:");
  for (const failure of failures) console.error(`- ${failure}`);
  process.exitCode = 1;
} else {
  console.log(`Agent compatibility verified for ${skillNames.length} skills.`);
}
