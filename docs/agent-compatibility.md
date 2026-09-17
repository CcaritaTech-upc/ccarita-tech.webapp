# Convergent Testing agent compatibility

The canonical runtime package lives in `.agents/skills/`. Cursor, Codex, and Antigravity discover that directory natively. Tool-specific files only bridge discovery or provide convenient entry points.

## Quick path

1. Start the agent from the repository root.
2. Request a new feature or explicitly invoke the Convergent Testing skill.
3. Confirm the agent reports G0-G4 evidence before calling the feature complete.
4. Run `node scripts/verify-agent-compatibility.mjs` after changing any agent integration file.

## Compatibility matrix

| Tool | Persistent rule | Skill discovery | Explicit entry point |
|---|---|---|---|
| OpenCode | `AGENTS.md` | `opencode.json` points to `.agents/skills/` | `/convergent-gate <feature>` |
| Claude Code | `CLAUDE.md` imports `AGENTS.md` | `.claude/skills/` wrappers load `.agents/skills/` | `/convergent-testing` |
| Cursor | Native `AGENTS.md` support | Native `.agents/skills/` support | `/convergent-testing` |
| Codex | Native `AGENTS.md` support | Native `.agents/skills/` support | `$convergent-testing` or `/skills` |
| Antigravity | `.agents/rules/convergent-testing.md` | Native `.agents/skills/` support | Mention `convergent-testing` or delegate to `convergent-qa` |

## Design decisions

- `.agents/skills/` is the single source of truth because three target agents implement that shared project path.
- Claude Code uses wrappers rather than symlinks so repository checkout works consistently on Windows without Developer Mode or administrator privileges.
- OpenCode uses `skills.paths` rather than a copied skill tree.
- OpenCode may report a Claude wrapper as a discovered location because it also imports Claude-compatible project skills. The wrapper delegates to the same canonical `.agents/skills/` file, and the verifier checks every target.
- Antigravity gets a workspace rule and a custom subagent. No workflow is added because Antigravity workflows are deprecated in favor of Agent Skills by November 1, 2026.
- Human documentation under `convergent-testing/` is explanatory only. Runtime skills do not depend on it.

## Runtime context boundaries

The skills remain product-agnostic. At runtime they discover project-owned context rather than embedding it:

1. project delivery discipline (for example `docs/delivery-discipline.md`);
2. bounded-context business rules and evidence ledgers;
3. the actor matrix for transversal journeys;
4. commands, results, skip reasons, and owned open risks.

Tool-specific wrappers must never copy product rules. They load the canonical skill, which then reads the active project's records.

## Verification

The repository check validates canonical skills, Claude wrappers, persistent rules, references, and OpenCode entry points. CI runs the same command in the `agent-compatibility` job:

```bash
node scripts/verify-agent-compatibility.mjs
```

Native discovery should also be checked with each installed tool. A missing local CLI is an environment limitation, not proof of discovery.

The local `agy` CLI available during setup was version 1.1.27 and returned no custom-agent listing. Antigravity integration therefore follows the official Antigravity 2.0 paths and schema but still needs an interactive discovery check in a current Antigravity IDE.
