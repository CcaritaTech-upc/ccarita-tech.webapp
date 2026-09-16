# Convergent Testing Framework

Convergent Testing is a flow-first, risk-layered, agent-assisted testing methodology. It verifies software at the cheapest trustworthy layer, then progressively validates the boundaries between layers until the complete user journey is proven end to end.

> **Status:** Experimental. IoBuild is the first pilot and will be used to challenge and refine the methodology before it becomes an agent skill or executable tool.

## Core idea

```text
Specific evidence                    System evidence

Frontend ────────┐
                 ├── Frontend + API ──────┐
Backend ────┬────┘                         ├── System journey
            └── Backend + Database ────────┘
Database ───┘
```

Convergence is progressive. A system test is not the first place where incompatible assumptions should be discovered.

## Quick path

1. Define a user-visible outcome as a **System Journey**.
2. Describe its happy path and observable acceptance criteria.
3. Identify risks and classify scenarios into tiers.
4. Assign each scenario to the cheapest layer capable of proving it.
5. Verify pairwise boundaries before running the global E2E scenario.
6. Capture diagnostic evidence when a test fails.
7. Use an agent and browser automation for investigation, but require a deterministic coded test as final proof.

## Documentation map

| Document | Purpose |
|----------|---------|
| [Vision](00-vision.md) | Goals, principles, scope, and non-goals |
| [Core model](01-core-model.md) | Concepts, taxonomy, identifiers, and scenario metadata |
| [System journeys](02-system-journeys.md) | How to discover, define, and evolve a journey |
| [Verification layers](03-verification-layers.md) | Responsibilities and convergence points |
| [Risk tiers](04-risk-tiers.md) | Scenario prioritization and execution cadence |
| [Forms and business rules](05-forms-and-business-rules.md) | Field, cross-field, contextual, and business validation |
| [Agent diagnostic loop](06-agent-diagnostic-loop.md) | Safe MCP-assisted investigation and deterministic regression |
| [Evidence and gates](07-evidence-and-quality-gates.md) | Required artifacts, quality gates, and completion criteria |
| [IoBuild IAM pilot](pilots/iobuild-iam.md) | First real application of the framework |
| [Journey template](templates/journey-template.md) | Reusable starting point for new journeys |

## Planned evolution

```text
Documented methodology
        ↓
IoBuild pilot
        ↓
Refinement from real failures
        ↓
Project-local agent skill
        ↓
Validated agent workflow
        ↓
Project-local CLI / OpenCode integration
```

Automation follows validation. The methodology must survive real delivery work before its decisions are encoded into tools.

## Non-goals

Convergent Testing is not:

- a replacement for existing test runners;
- a requirement to duplicate every scenario at every layer;
- a self-healing mechanism that silently changes expectations;
- a frontend-only Playwright convention;
- a coverage-percentage optimization exercise.

It is an orchestration model for deciding **what to prove, where to prove it, how evidence converges, and how failures become durable regressions**.
