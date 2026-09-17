# Convergent Testing gate protocol

## Progressive convergence

```text
Frontend internals ─┐
                    ├─ Frontend/API contract ─┐
Backend logic ──────┼─ Backend/database ──────┼─ System Journey E2E
Persistence ────────┘                          ┘
```

## Verification ownership

| Layer | Owns |
|---|---|
| Domain | Pure invariants and state transitions |
| Application | Use-case orchestration and failure decisions |
| Component | Rendering, interaction, accessible feedback |
| Frontend integration | Component/store/router/API-client collaboration |
| API | Routing, binding, auth, status and error mapping |
| Contract | Request/response/event compatibility |
| Persistence | Constraints, transactions, concurrency, migrations, durability |
| System/E2E | User-visible outcome and deployed wiring |

Security, resilience, accessibility, performance, and observability are cross-cutting dimensions.

## Actor coverage

For a journey serving multiple actors, require one happy-path variant per actor at the highest applicable layer. Share lower-layer evidence only when route, permissions, data, and outcome are genuinely identical.

## Risk tiers

- **A:** common or critical; blocks delivery.
- **B:** plausible degradation with meaningful impact.
- **C:** uncommon boundary or operational condition.
- **D:** deterministic paranoia campaigns: malformed data, mutation targets, fuzz partitions, fault injection, and resource pressure.

Prioritize by likelihood × impact × difficulty of detection. Rare security, privacy, money, or irreversible data risks remain Tier A.

## Gate evidence

- **G0:** local tests and deterministic inputs.
- **G1:** executable contracts and production-engine tests where guarantees depend on infrastructure.
- **G2:** coded E2E, clean state, trace/screenshot/network evidence on failure.
- **G3:** required tiers and explicit exclusions.
- **G4:** clean rerun without MCP, no unexplained retry, evidence ledger.
- **Environment fidelity:** assert failure-body leakage against production behavior (development hosts print diagnostics by design); prove infrastructure guarantees against the production engine.

Retries expose instability; they do not convert flaky behavior into proof.

A `passed` gate requires a command and result. A `skipped` gate requires a reason. Open risks require an owner and review date. Local-only execution does not satisfy a CI-required delivery gate.
