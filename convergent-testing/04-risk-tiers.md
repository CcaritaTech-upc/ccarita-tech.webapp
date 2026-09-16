# Risk tiers and scenario selection

Risk tiers determine which what-if scenarios deserve deeper coverage and when they run. They must consider business impact, not only how frequently a failure occurs.

## Risk model

Evaluate at least three factors:

```text
Risk = likelihood × impact × difficulty of detection
```

Numeric scoring may help comparison, but product judgment remains necessary. A rare privilege escalation or irreversible data-loss scenario belongs in the highest tier even if its measured probability is low.

## Default tiers

| Tier | Meaning | Examples | Default cadence |
|------|---------|----------|-----------------|
| **A** | Critical or common; blocks delivery | Authorization bypass, duplicate payment, invalid common input, transaction corruption | Every pull request where practical |
| **B** | Plausible degradation with meaningful impact | Timeout, token expiration, dependency `500`, interrupted navigation | Main branch or focused pull requests |
| **C** | Uncommon boundary or operational condition | Unicode/collation edge, lock contention, stale multi-tab state | Nightly or scheduled |
| **D** | Exploratory and extreme “QA paranoia” | Corrupt responses, long-running races, resource exhaustion, unusual protocol sequences | Scheduled, manual, or campaign-based |

Tier D is not disposable. It is a discovery space for fuzzing, property-based testing, mutation testing, fault injection, security probes, and chaos experiments. A discovered high-impact defect should promote its regression scenario to a higher tier.

## Scenario portfolio rules

1. Complete the happy path before expanding what-if tiers.
2. Include common user mistakes in Tier A.
3. Elevate security, money, privacy, and irreversible data risks.
4. Test exhaustive input partitions below E2E.
5. Promote every production incident to a deterministic regression tier.
6. Remove or redesign scenarios that produce no distinct evidence.

## Suggested execution gates

```text
Pull request: fast layers + Tier A + minimal E2E smoke
Main branch: Tier A/B convergence suite
Nightly: Tier C, browser matrix, real integrations where safe
Scheduled campaign: Tier D, load, fuzzing, mutation, chaos
```

Cadence is configurable per project. Tier meaning must remain stable so reports are comparable.

## Prioritization checklist

- Could the failure expose or corrupt data?
- Could it grant an unauthorized capability?
- Could it charge money incorrectly?
- Would the user be blocked from a primary journey?
- Is the failure difficult to observe before production?
- Has it happened before?
- Can a cheaper layer prove it reliably?
