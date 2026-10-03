# Profiles evidence ledger

## Backend test navigation — 2026-10-02

The [Profiles catalogue](test-catalog.md) maps creation, management and photo
coverage by actual layer/dependency. [Reorganization evidence](../../../backend/tests/reorganization-evidence.md)
records source/discovery preservation and local runs; no new live-MySQL,
Cloudinary or CI acceptance is claimed by this organization-only entry.

See the [crosscutting frontend performance report](../../performance/evidence-ledger.md)
for shared gzip/cache/chunks, functional E2E evidence and the new local
mobile/desktop Lighthouse audit of `/profiles/profile` for Builder and Owner.
The original two-route IAM comparison is preserved separately. Local observations
do not establish CI acceptance or all-state performance coverage.

Surgical round two links both-role fields/language controls and improves shared
action contrast: A100 for both measured profile views, with save/reload/locale
regressions in the repeated full E2E suite. Details and CI limits are central.

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
owner_assignment_regression:
  cause: Owner profile E2E attempted registration without provisioning an assigned unit after IAM eligibility changed; Next correctly stayed disabled.
  repair: Owner fixture now provisions an actual builder, project, unit, and client assignment before the UI registration.
  commands:
    - command: E2E_BASE_URL=http://localhost:8081 npx playwright test tests/e2e/profiles-manage.spec.js tests/e2e/iam-happy-path.spec.js tests/e2e/iam-form-feedback.spec.js
      result: 7/7 passed against isolated MySQL 8 Compose stack.
    - command: E2E_BASE_URL=http://localhost:8081 npx playwright test
      result: 13/13 passed against isolated MySQL 8 Compose stack.
  delivery_note: CI rerun remains pending; local evidence is not CI evidence.
```

## Builder years-in-business profile field

```yaml
context: profiles
feature: builder-years-in-business-profile-field
journey: Builder registers, retrieves, and edits years in business independently from personal age; Owner age data remains unchanged.
actor_coverage:
  - actor: Builder
    outcome: Integer years 0–120 persist in `Profile.YearsInBusiness`, are returned by the profile API, and survive profile edit/reload.
  - actor: Owner
    outcome: `Profile.Age` continues to store Owner age; `YearsInBusiness` remains null.
scenarios:
  - tier: A
    scenario: Profile API rejects supplied years below 0 or above 120 and does not map Builder years into Age.
    owner: backend/tests/Modules/Profiles/Management/Api/ProfileAccessTests.cs
  - tier: A
    scenario: Existing MySQL profiles schema gains the nullable column and moves valid legacy Builder Age values into YearsInBusiness.
    owner: backend/tests/Modules/Profiles/Management/Persistence/ProfilePersistenceMySqlTests.cs
  - tier: B
    scenario: Builder registration with zero persists age=null and yearsInBusiness=0; profile view edit and reload preserve the updated value.
    owner: frontend/tests/e2e/iam-builder.spec.js, frontend/tests/e2e/profiles-manage.spec.js
layer_ownership:
  G0: Validator, ProfileAssembler, service-range, and API-range checks run in Vitest/xUnit.
  G1: MySQL 8 proves profile creation/update durability and the legacy-column upgrade/backfill.
  G2: Playwright covers Builder registration, Profile GET, profile edit/reload, and Owner age regression.
  G3: Boundary values and legacy schema upgrade have explicit API/persistence assertions.
  G4: The focused Builder registration/profile journeys repeat cleanly; full suite reruns are recorded below.
gates:
  G0: passed
  G1: skipped — local MySQL 8 persistence/migration tests pass; CI has not run on this worktree.
  G2: skipped — local full-stack E2E evidence is green; CI has not run on this worktree.
  G3: skipped — local Tier A and MySQL proofs passed; CI acceptance is pending.
  G4: skipped — deterministic local reruns passed; CI delivery evidence is pending.
commands:
  - command: npm run test:unit
    result: 77/77 passed, including ProfileAssembler mapping and the 0-years boundary.
  - command: npm run build
    result: passed.
  - command: dotnet test backend/IoBuild.sln --no-restore --verbosity minimal
    result: 241/241 passed; opt-in MySQL tests skip when no dedicated connection is configured.
  - command: IOBUILD_TEST_MYSQL_CONNECTION=... dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter FullyQualifiedName~ProfilePersistenceMySqlTests --verbosity minimal
    result: 4/4 passed against MySQL 8, including schema upgrade/backfill and age/years roundtrip.
  - command: E2E_BASE_URL=http://127.0.0.1:18081 E2E_NGINX=1 E2E_CLOUDINARY_DUMMY=1 E2E_SIMULATED_PAYMENTS=1 npm run test:e2e -- --workers=2
    result: 30/30 passed against a clean isolated MySQL 8 stack.
  - command: same E2E base/flags with `--workers=1 --repeat-each=2 --grep "IAM Builder happy path|PROFILES Builder:|PROFILES Owner:"`
    result: 6/6 passed across repeated Builder registration, Builder profile edit/reload, and Owner profile journeys.
diagnostic_verdicts:
  - failure: An initial backend test compile found the migration-upgrade test method duplicated in ProfilePersistenceMySqlTests.
    evidence: The compiler reported CS0111 for `Migration_adds_years_in_business_and_backfills_legacy_builder_values`.
    verdict: Removed the duplicate test body; targeted suite, full solution, and MySQL persistence suite then passed.
open_risks:
  - what: Legacy Builder Age values outside 0–120 are not copied into YearsInBusiness by the upgrade; the old Age value remains for review.
    why: Values outside the business-years domain must not be migrated into the new field or discarded automatically.
    owner: ccarita-tech
    who: ccarita-tech
    when: 2026-10-08
    review_by: 2026-10-08
```
