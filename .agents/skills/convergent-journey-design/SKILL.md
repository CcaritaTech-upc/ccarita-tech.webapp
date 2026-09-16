---
name: convergent-journey-design
description: "Trigger: journey design, feature acceptance, risk matrix. Converts a feature into testable outcomes, layers, and risk-tier scenarios."
license: Apache-2.0
metadata:
  author: "ccarita-tech"
  version: "0.3"
---

## Activation Contract

Use before implementing behavior or when acceptance criteria are incomplete.

## Hard Rules

- Define one actor, trigger, outcome, preconditions, and durable postconditions.
- Use implementation-neutral language for the journey.
- Separate business rules from UI feedback.
- Assign each assertion one primary layer.
- Use explicit decision records, not hidden reasoning traces.

## Decision Gates

| Ambiguity | Action |
|---|---|
| Changes user-visible behavior | Ask one question and stop |
| Only affects technical placement | Choose the cheapest trustworthy layer and record it |
| Rare catastrophic risk | Promote to Tier A |

## Execution Steps

1. State `Actor → Trigger → Outcome`.
2. Write happy path and observable acceptance criteria.
3. Identify states, transitions, trust boundaries, and dependencies.
4. Generate what-if scenarios from field, cross-field, authorization, temporal, concurrency, dependency, and recovery risks.
5. Classify A-D and map assertions to layers.
6. Emit the scenario schema expected by the orchestrator.

## Output Contract

Return one journey record, risk matrix, layer ownership table, convergence points, and explicit unknowns.
