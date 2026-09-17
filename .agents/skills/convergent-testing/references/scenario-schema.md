# Scenario and evidence schemas

## Scenario record

```yaml
journey: DOMAIN.JOURNEY
actor: ACTOR_ID
scenario: SCENARIO_ID
title: Observable behavior
kind: happy | what-if
risk:
  tier: A | B | C | D
  likelihood: low | medium | high
  impact: low | medium | high | critical
  detectability: low | medium | high
preconditions: []
stimulus: []
expected_outcomes: []
layers: []
convergence_points: []
evidence: []
```

## Evidence ledger

```yaml
journey: DOMAIN.JOURNEY
feature: short-name
gates:
  G0: passed | failed | skipped
  G1: passed | failed | skipped
  G2: passed | failed | skipped
  G3: passed | failed | skipped
  G4: passed | failed | skipped
skip_reasons: []
commands:
  - command: exact command
    result: verifiable result
artifacts: []
failures:
  - class: product | test | environment | flaky | specification | unknown
    evidence_for: []
    evidence_against: []
    verdict: string
open_risks:
  - risk: string
    owner: required
    review_by: required
roles_covered:
  - actor: ACTOR_ID
    happy_path: evidence reference
```

Every `passed` gate requires command evidence. Every `skipped` gate requires a reason tied to scope. Every failure must preserve the first failing evidence before repair. Every open risk requires an owner and review date. Every served actor requires happy-path evidence.
