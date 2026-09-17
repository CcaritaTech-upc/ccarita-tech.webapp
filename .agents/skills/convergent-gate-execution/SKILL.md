---
name: convergent-gate-execution
description: "Trigger: run validation gate, verify feature, feature complete. Executes layered tests and returns deterministic Convergent Testing evidence."
license: Apache-2.0
metadata:
  author: "ccarita-tech"
  version: "0.4"
---

## Activation Contract

Use after implementation or before claiming a behavior change complete.

## Hard Rules

- Run cheapest layers before expensive system tests.
- Preserve the first failure artifacts.
- Use clean data and browser context for system tests.
- Never treat retries, MCP interaction, or manual observation as final proof.
- Do not run irrelevant gates; record why they were skipped.
- Every open risk needs an owner and a review date.
- A `passed` gate needs a command plus a verifiable result.
- When a journey serves multiple actors, prove the happy path per actor.

## Decision Gates

| Scope | Minimum evidence |
|---|---|
| Pure rule | G0 unit/domain |
| API or persistence behavior | G0 + G1 |
| Critical user-visible journey | G0 + G1 + G2 + relevant G3 |
| Release-sensitive change | All relevant gates + G4 clean rerun |

## Execution Steps

1. Discover project test commands without changing dependencies.
2. Run G0 and record exact command/result.
3. Run contracts and production-engine checks required by G1.
4. Run the smallest deterministic happy-path E2E for G2.
5. Run required risk tiers for G3.
6. Rerun affected evidence without MCP for G4.
7. Emit the evidence ledger.

## Output Contract

Return a gate table with pass/fail/skip, commands, counts, durations, artifacts, flaky observations, and open risks. A red required gate means `NOT COMPLETE`.
