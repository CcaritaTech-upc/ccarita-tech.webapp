# Evidence ledger template

Copy this file to `docs/bounded-contexts/<context>/evidence-ledger.md` and
fill it per bounded context. Delete nothing; mark unknown items explicitly.

```yaml
context: BOUNDED_CONTEXT
status: piloted | not-piloted
journey: DOMAIN.JOURNEY
feature: short-name
gates:
  G0: passed | failed | skipped
  G1: passed | failed | skipped
  G2: passed | failed | skipped
  G3: passed | failed | skipped
  G4: passed | failed | skipped
skip_reasons:
  - gate: G2
    reason: why this gate does not apply or cannot run yet
commands:
  - command: exact command that was run
    result: counts, exit code, duration
  - command: node scripts/verify-agent-compatibility.mjs
    result: Agent compatibility verified for 4 skills.
artifacts:
  - path or location of traces, screenshots, logs
failures:
  - class: product | test | environment | flaky | specification | unknown
    evidence_for: []
    evidence_against: []
    verdict: string
open_risks:
  - risk: what remains unproven
    owner: who resumes it (required, never blank)
    review_by: date or milestone (required, never blank)
roles_covered:
  - role: each actor served by the journey
    happy_path: where its evidence lives
```

Rules (see `docs/delivery-discipline.md`):

- Every `skipped` gate needs a reason tied to scope or environment.
- Every `open_risk` needs an owner and a review date.
- Every `failed` gate must preserve the first failing evidence before repair.
- Transversal journeys list every served role with its own happy-path evidence.
- Anything not running in CI is `skipped`, never `passed`.
