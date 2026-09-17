# Profiles evidence ledger

```yaml
context: profiles
status: piloted
journey: PROFILES.MANAGE (Builder and Owner variants)
feature: profiles-convergence
gates:
  G0: passed
  G1: passed
  G2: passed
  G3: passed
  G4: passed
environment:
  database: mysql:8.0 (production engine for all persistence proofs)
  migrations: 202608280001_FoundationSchema → 202608290002_IamAndDispatch → 202608290003_CoreBusiness → 202608300004_DevicesTelemetry → 202608300005_AnalyticsProjections → 202609170006_SubscriptionActiveArbiter
  backend: .NET 9 (CI setup-dotnet 9.0.x)
  frontend: node 22 (CI setup-node 22)
reruns:
  - command: dotnet test backend/IoBuild.sln with live MySQL (temporary 3306:3306 mapping, reverted afterwards)
    result: 185/185 green (15 architecture + 20 contract + 41 integration + 109 modules)
  - command: E2E_BASE_URL=http://localhost:8081 npx playwright test against the freshly built api image
    result: 7/7 green (IAM, subscriptions, and both profiles journeys); e2e evidence rows cleaned afterwards
flaky_rate:
  observed: 0 unexplained flakes across all reruns above
mutations:
  - mutant: ownership checks removed (any user reads/writes any profile)
    killed_by: CREATE_OWN/CREATE_FOR_OTHER, READ_scoped, UPDATE_OWN/OTHER, PHOTO ownership tests
  - mutant: duplicate-create guard removed (unique violation escapes as 500)
    killed_by: Duplicate_create_for_same_user_conflicts (proven red 500 without the catch during development)
  - mutant: photo bootstrap removed (null never matches)
    killed_by: PHOTO_replace first-replacement-204 assertion
  - mutant: partial-update semantics removed (blank name overwrites)
    killed_by: UPDATE_OWN name-preservation assertion
skip_reasons: []
commands:
  - command: dotnet test backend/tests/Modules --filter ProfileAccessTests
    result: 5/5 passed (create/read/update ownership, photo compare-and-swap with fake uploader, failed-upload abort)
  - command: npm run test:unit (frontend)
    result: 27/27 passed (validators 7/7 + iam-contract 7/7 + subscriptions-contract 7/7 + profiles-contract 6/6, last local run)
  - command: IOBUILD_TEST_MYSQL_CONNECTION=... dotnet test --filter ProfilePersistenceMySqlTests (temporary 3306:3306 mapping, reverted afterwards)
    result: 3/3 passed against live MySQL 8.0 (create/read/update roundtrip, duplicate create 409 with single row, photo swap durability); probe rows cleaned
  - command: E2E_BASE_URL=http://localhost:8081 npx playwright test (deployed nginx + dist + API + MySQL)
    result: profiles-manage.spec.js 2/2 green (Builder and Owner register→view→update→reload); full suite 7/7 with IAM and subscriptions journeys; e2e evidence rows cleaned afterwards
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
  - risk: photo endpoint has no UI callers; proven at API level only
    owner: ccarita-tech
    review_by: 2026-10-01
  - risk: parallel photo replaces race (last-writer-wins, single row always intact; probed 6-way with one winner and five clean rejections)
    owner: ccarita-tech
    review_by: 2026-10-01
    disposition: accepted as benign, do not fix without a real incidence
roles_covered:
  - role: Builder
    happy_path: frontend/tests/e2e/profiles-manage.spec.js (Builder test)
  - role: Owner
    happy_path: frontend/tests/e2e/profiles-manage.spec.js (Owner test)
```
