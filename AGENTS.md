# Project agent rules

## Mandatory Convergent Testing Gate

Every new feature is incomplete until it passes the Convergent Testing Gate. Agents must load the project-local `convergent-testing` skill before implementing or validating a feature.

Required lifecycle:

1. Define the System Journey and observable acceptance criteria.
2. Identify affected frontend, backend, persistence, contract, and external boundaries.
3. Classify happy-path and what-if scenarios by risk tier.
4. Assign each assertion to the cheapest trustworthy verification layer.
5. Implement the feature and its owned tests together.
6. Run local layers, pairwise convergence, and required system/E2E evidence.
7. If any gate fails, load `convergent-failure-diagnosis`; do not weaken assertions to obtain green output.
8. Report commands, results, unresolved risks, and skipped gates before claiming completion.

The gate may be proportionate: documentation-only changes do not require E2E, but behavior changes require deterministic regression evidence. MCP or interactive browser success is diagnostic evidence only; final acceptance must come from versioned tests running without MCP.

## Gate levels

- **G0 Local:** domain, application, unit, and component evidence.
- **G1 Boundaries:** frontend/API contracts and backend/database integration where relevant.
- **G2 System:** happy-path E2E for user-visible critical journeys.
- **G3 Risk:** required Tier A plus applicable Tier B-D campaigns.
- **G4 Delivery:** deterministic rerun, artifacts, and no unexplained flakiness.

## Agent integration

- Canonical runtime skills live in `.agents/skills/`; do not duplicate the protocol elsewhere.
- OpenCode: use `/convergent-gate <feature>`.
- Claude Code and Cursor: use `/convergent-testing`.
- Codex: use `$convergent-testing` or select it through `/skills`.
- Antigravity: mention `convergent-testing` or delegate to `convergent-qa`.

Run `node scripts/verify-agent-compatibility.mjs` after changing agent integration files.

## Delivery discipline

Follow `docs/delivery-discipline.md`. Business rules and evidence ledgers live in `docs/bounded-contexts/<context>/`. Keep skills and this file free of product content.
