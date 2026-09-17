# Profiles evidence ledger

```yaml
context: profiles
status: piloted
journey: PROFILES.MANAGE (Builder and Owner variants)
feature: profiles-convergence
gates:
  G0: passed
  G1: passed
  G2: skipped
  G3: skipped
  G4: skipped
skip_reasons:
  - gate: G2
    reason: no per-actor profile E2E yet
  - gate: G3
    reason: no tiered portfolio yet
  - gate: G4
    reason: no rerun, flaky, or mutation evidence yet
commands:
  - command: dotnet test backend/tests/Modules --filter ProfileAccessTests
    result: 5/5 passed (create/read/update ownership, photo compare-and-swap with fake uploader, failed-upload abort)
  - command: npm run test:unit (frontend)
    result: 27/27 passed (validators 7/7 + iam-contract 7/7 + subscriptions-contract 7/7 + profiles-contract 6/6, last local run)
  - command: IOBUILD_TEST_MYSQL_CONNECTION=... dotnet test --filter ProfilePersistenceMySqlTests (temporary 3306:3306 mapping, reverted afterwards)
    result: 3/3 passed against live MySQL 8.0 (create/read/update roundtrip, duplicate create 409 with single row, photo swap durability); probe rows cleaned
artifacts: []
failures:
  - class: product
    evidence_for: [duplicate profile create for the same user threw an unhandled unique violation to a 500]
    evidence_against: [unique UserId index exists; one profile per user]
    verdict: catch 1062 in the create endpoint and answer 409; covered by the MySQL duplicate test keeping a single row
  - class: product
    evidence_for: [all 5 profile endpoints required login but enforced zero ownership: any user could read or edit anyone's PII, create foreign profiles, and replace foreign photos]
    evidence_against: [profiles hold address, phone, age, and second email]
    verdict: RequireAuthorization plus token-id ownership on every endpoint (403 on explicit mismatch, 404 on foreign ids, list scoped to caller); photo compare-and-swap kept, frontend untouched (it always used own ids)
  - class: product
    evidence_for: [fresh profiles start with null PhotoReference, which no client string can match, so the photo endpoint could never succeed]
    evidence_against: [endpoint exists with a fake-testable uploader seam and no frontend callers]
    verdict: null bootstraps as empty for the first replacement; afterwards the swap stays strict; covered by the 204-then-409 test
open_risks:
  - risk: no per-actor profile E2E yet (Builder and Owner views)
    owner: ccarita-tech
    review_by: 2026-10-01
  - risk: photo endpoint has no UI callers; proven at API level only
    owner: ccarita-tech
    review_by: 2026-10-01
roles_covered: []
```
