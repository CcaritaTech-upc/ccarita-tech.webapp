---
name: convergent-testing
description: "Trigger: new feature, convergent testing, validation gate, system journey. Orchestrates self-contained flow-first testing before feature completion."
license: Apache-2.0
metadata:
  author: "ccarita-tech"
  version: "0.4"
---

## Activation Contract

Use for every new behavior or feature and whenever the user requests a testing gate. Orchestrate journey design, layered evidence, risk coverage, diagnosis, and deterministic acceptance.

## Hard Rules

- Treat a feature as incomplete until its gate passes.
- Start from observable outcomes, never test folders or tools.
- Give every assertion one owning layer; sample lower rules in E2E, never duplicate them exhaustively.
- Keep backend business rules authoritative and use production infrastructure for infrastructure guarantees.
- Never change an expectation merely to make a test green.
- MCP and browser exploration diagnose only; versioned tests provide final proof.
- Do not request or expose hidden chain-of-thought. Record decisions, evidence, hypotheses, and verdicts instead.
- Keep this skill product-agnostic; load project discipline, business rules, and ledgers from project documentation when present.
- Enumerate every actor served by a transversal journey and require happy-path evidence per actor.

## Decision Gates

| Condition | Required action |
|---|---|
| Journey or expected outcome unclear | Load `convergent-journey-design`; stop before implementation if ambiguity changes behavior |
| Feature changes behavior | Require G0 and relevant G1; add G2 for critical user-visible journeys |
| Failure blocks a gate | Load `convergent-failure-diagnosis` |
| Evidence is ready | Load `convergent-gate-execution` for deterministic verification |

## Execution Steps

1. Load applicable project discipline and bounded-context records without copying product content into this skill.
2. Produce a journey record, actor matrix, and risk matrix.
3. Map assertions to layers and convergence points.
4. Implement behavior and tests as one work unit.
5. Execute G0→G4 in order, skipping only demonstrably irrelevant gates.
6. Diagnose failures with bounded hypothesis branching and rerun without MCP.
7. Return the evidence ledger and unresolved risks.

## Output Contract

Return `journey`, `actor_coverage`, `scenarios_by_tier`, `layer_ownership`, `commands_run`, `gate_results`, `artifacts`, `diagnostic_verdicts`, and owned `open_risks`. Never say complete when a required gate is skipped, red, or missing actor coverage.

## References

- `references/gate-protocol.md`
- `references/scenario-schema.md`
