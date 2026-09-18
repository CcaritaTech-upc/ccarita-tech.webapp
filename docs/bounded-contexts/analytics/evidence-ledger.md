# Analytics evidence ledger

```yaml
context: analytics
status: piloted
journey: ANALYTICS.VIEW (Builder dashboard and Owner dashboard variants)
feature: analytics-convergence
gates:
  G0: passed
  G1: passed
  G2: skipped
  G3: skipped
  G4: skipped
skip_reasons:
  - gate: G2
    reason: no per-actor dashboard E2E yet
  - gate: G3
    reason: no tiered portfolio yet
  - gate: G4
    reason: no rerun, flaky, or mutation evidence yet
commands:
  - command: dotnet test backend/tests/Modules --filter AnalyticsAccessTests
    result: 2/2 passed (metrics/energy self-match with 403 cross and 401 anonymous; insights scoped to owned projects)
  - command: npm run test:unit (frontend)
    result: 39/39 passed (validators 7/7 + iam-contract 7/7 + subscriptions-contract 7/7 + profiles-contract 6/6 + devices-contract 5/5 + publishing-contract 4/4 + analytics-contract 3/3, last local run)
  - command: dotnet test backend/tests/Modules --filter AnalyticsDashboardTests
    result: 3/3 passed (builder and owner dashboard data, energy window clamp)
  - command: IOBUILD_TEST_MYSQL_CONNECTION=... dotnet test --filter Dashboards_and_projections_are_durable_on_mysql (temporary 3306:3306 mapping, reverted afterwards)
    result: 1/1 passed against live MySQL 8.0 (both dashboards with data plus project/unit/device projection durability); probe rows cleaned
artifacts: []
failures:
  - class: product
    evidence_for: [all 5 analytics routes answered anonymously for any user id, including PII-adjacent consumption metrics]
    evidence_against: [every frontend caller passes its own user id]
    verdict: RequireAuthorization plus token-id self-match on metric/energy routes (403 on mismatch); insights scoped to builder-owned or unit-occupied projects (404 otherwise)
open_risks:
  - risk: no per-actor dashboard E2E yet (Builder and Owner views)
    owner: ccarita-tech
    review_by: 2026-10-01
roles_covered: []
```
