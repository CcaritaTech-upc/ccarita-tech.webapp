# Subscriptions evidence ledger

See the [crosscutting frontend performance report](../../performance/evidence-ledger.md)
for shared gzip/cache/chunks, deferred Stripe initialization and the new local
mobile/desktop Lighthouse audit of Builder `/subscriptions/my-subscription`
with its embedded plans catalog. Payments are simulated; these observations do
not establish CI or real-provider acceptance. Historical evidence is preserved.

Surgical round two fixes `/subscriptions` to its canonical nested destination;
unit/direct-entry/real-menu and repeated purchase evidence are in the central
report. Providers remain simulated locally and CI is pending.

```yaml
context: subscriptions
status: piloted
journey: SUBSCRIPTIONS.PURCHASE (Builder-only)
feature: subscriptions-convergence
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
    result: 170/170 green on consecutive runs (15 architecture + 20 contract + 41 integration + 94 modules)
flaky_rate:
  observed: 0 unexplained flakes across all reruns above
mutations:
  - mutant: restricted-key gate removed (any key resolves)
    killed_by: CHECKOUT_WITHOUT_KEY_fails_closed + Outgoing_calls_carry_the_restricted_key
  - mutant: plan-existence check removed (unknown plan creates session)
    killed_by: CHECKOUT_UNKNOWN_PLAN_is_rejected
  - mutant: webhook JSON guard removed (malformed body throws)
    killed_by: WEBHOOK_MALFORMED_JSON_is_rejected_without_server_error
  - mutant: single-active arbiter removed (concurrent confirms double up)
    killed_by: Concurrent_confirms_of_one_session_leave_a_single_active (probe confirmed 6 actives without it)
  - mutant: supersede-expiry removed (old actives linger)
    killed_by: PURCHASE_SUPERSEDE_expires_previous_active_subscription
  - mutant: webhook idempotency removed (duplicate event duplicates rows)
    killed_by: WEBHOOK_IDEMPOTENCY_keeps_a_single_subscription_row
skip_reasons: []
commands:
  - command: dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --filter "FullyQualifiedName~StripeKeyDisciplineTests"
    result: 4/4 passed (restricted-key resolution, fail-closed secrets, outgoing Authorization header)
  - command: npm run test:unit (frontend)
    result: 21/21 passed (validators 7/7 + iam-contract 7/7 + subscriptions-contract 7/7, last local run)
  - command: dotnet test backend/tests/Modules --filter purchase/subscription/key-discipline suites
    result: 10/10 passed (5 purchase flow incl. webhook idempotency, 1 MySQL activation+supersede, 4 key discipline)
  - command: IOBUILD_TEST_MYSQL_CONNECTION=... dotnet test --filter SubscriptionPersistenceMySqlTests (temporary 3306:3306 mapping, reverted afterwards)
    result: 1/1 passed against live MySQL 8.0 (activation plus supersede expiry with EndDate); probe rows cleaned, table left without test residue
  - command: E2E_BASE_URL=http://localhost:8081 npx playwright test (deployed nginx + dist + API + MySQL)
    result: 5/5 passed — IAM journeys plus SUBSCRIPTIONS purchase (browse plans, simulated pay, active Starter); e2e evidence rows cleaned afterwards
  - command: same suite against real Stripe (rk_ key, UseSimulatedPayments=false)
    result: 7/7 passed — purchase pays with the 4242 test card on checkout.stripe.com (billing name required) and confirms to active; profiles journeys included; e2e evidence rows cleaned afterwards
  - command: CI run on main with the purchase spec (frontend-e2e job against the compose stack)
    result: success after scoping the dummy Stripe key to job level (first attempt failed with no checkout redirect because the dist built without the key)
artifacts: []
failures:
  - class: product
    evidence_for: [receipts visible only with mapped customers; one-off payments leave no invoice, so their receipts stayed unreachable]
    evidence_against: [microservices version listed paid sessions with expanded charges]
    verdict: invoices read from paid checkout sessions (charge expanded, metadata-filtered, no customer mapping needed) ahead of the customer invoices list; unreachable Stripe answers null so the local fallback still applies; existing sequence-based provider test updated to the 3-call contract
  - class: product
    evidence_for: [invoices modal expects downloadUrl but neither the Stripe mapping nor the fallback ever sent any receipt URL]
    evidence_against: [frontend mapping receiptUrl-to-downloadUrl already present; microservices version sent ReceiptUrl from expanded charges]
    verdict: mapped Stripe hosted_invoice_url (fallback receipt_url) into PaymentInvoice.ReceiptUrl; frontend needs no change; local/synthesized invoices keep null and correctly show no button
  - class: product
    evidence_for: [anonymous purchase endpoints trusted client-provided builderId: cross-builder checkout, invoices, cancel, and full-list reads with no token required]
    evidence_against: [purchase is per-builder money flow; testing course grades IDOR]
    verdict: RequireAuthorization plus token-id self-match on every purchase endpoint (403 on mismatch, 404 on foreign ids, list scoped to caller); confirm still trusts Stripe metadata, webhook still HMAC-only; proof script and versioned tests updated to send tokens, E2E green against the enforced stack
  - class: product
    evidence_for: [AuthorizedRequest replaced the restricted key with any configured sk_ secret on outgoing Stripe calls]
    evidence_against: [resolver gates rk_ correctly; business rule requires least privilege]
    verdict: leftover pre-discipline behavior silently defeating the rk_ rule; fixed to send the restricted key only, covered by the outgoing-header test
  - class: product
    evidence_for: [empty Stripe configuration resolved rk_test_local and simulated payments successfully without charging]
    evidence_against: [business rule requires fail-closed without a key]
    verdict: fallback too generous; restricted to explicit simulation mode, empty config now 503s
  - class: product
    evidence_for: [checkout with unknown plan id returned 201 via price fallback]
    evidence_against: [paying for a nonexistent plan must not start]
    verdict: plan-existence check in the checkout endpoint, unknown plan now 404s
  - class: product
    evidence_for: [malformed webhook body threw JsonException to a 500]
    evidence_against: [failure paths must stay 4xx without internals]
    verdict: payload parse guard returning 400, covered by Tier A test
  - class: product
    evidence_for: [6 parallel confirms of one session created 6 active subscriptions]
    evidence_against: [exactly one active subscription per builder]
    verdict: check-then-act race; fixed with the single-active arbiter (generated column plus unique index, runner backfill, migration file) and 409 for the loser; stress-proven 4/4
open_risks:
  - risk: webhook path scheduled, not evidenced against real Stripe (needs live keys plus public URL)
    owner: ccarita-tech
    review_by: 2026-10-01
roles_covered:
  - role: Builder
    happy_path: frontend/tests/e2e/subscriptions-purchase.spec.js
```

## Confirmed subscription UI localization

```yaml
context: subscriptions
feature: localized-plan-catalog-and-subscription-screen
journey: Builder opens the subscription screen, compares plans, and completes the simulated Starter purchase in the selected language.
actor_coverage:
  - actor: Builder without a subscription
    outcome: Page header, no-subscription banner, available-plan descriptions, and purchase action use English by default.
  - actor: Builder with an active subscription
    outcome: Current-plan summary, usage count, renewal date, plan catalog, and comparison matrix switch between English and Spanish.
scenarios:
  - tier: B
    scenario: Seeded plan descriptions/features translate while unknown plan copy falls back to API text.
    owner: frontend/tests/unit/plan-copy.test.js
  - tier: B
    scenario: Subscription header, empty state, current-plan card, and comparison matrix switch locales without mixed-language hard-coded labels.
    owner: frontend/tests/e2e/builder-ui-localization.spec.js
  - tier: B
    scenario: No-subscription Builder sees English page copy and completes simulated Starter checkout.
    owner: frontend/tests/e2e/subscriptions-purchase.spec.js
layer_ownership:
  G0: Plan-copy fallback/mapping tests and validator tests run in Vitest.
  G1: Plan and subscription API contracts are unchanged; end-to-end purchase uses the seeded catalog on MySQL 8.
  G2: Playwright covers localized plan cards/comparison and Builder purchase happy path.
  G3: Payment provider behavior is explicitly simulated; no real Stripe acceptance is claimed by this UI change.
  G4: Two clean full-suite reruns passed (30/30 serial and 30/30 with two workers).
gates:
  G0: passed
  G1: passed
  G2: skipped — local Playwright evidence is green; CI has not run on this worktree.
  G3: skipped — payment was simulated locally; CI/real-provider acceptance is pending.
  G4: skipped — clean local reruns passed; CI delivery evidence is pending.
commands:
  - command: npm run test:unit
    result: 76/76 passed, including plan-copy tests.
  - command: npm run build
    result: passed.
  - command: E2E_BASE_URL=http://127.0.0.1:18081 E2E_NGINX=1 E2E_CLOUDINARY_DUMMY=1 E2E_SIMULATED_PAYMENTS=1 npm run test:e2e -- --workers=2
    result: 30/30 passed on the final clean isolated MySQL 8 run; a preceding clean single-worker full run also passed 30/30.
diagnostic_verdicts:
  - failure: Initial comparison-modal close locator matched both the PrimeVue header close icon and the content close button.
    evidence: Playwright strict mode reported two matching “Close” buttons after the translated comparison assertions passed.
    verdict: Scoped the locator to `.p-dialog-content .btn-close`; the locale-switching Builder journey passed on clean reruns.
  - failure: The initial purchase E2E expected Spanish “Elegir Starter” while the app's default locale is English.
    evidence: Playwright snapshot showed the plan button as “Choose Starter” and also surfaced Spanish static copy in the subscription page.
    verdict: Test expectation was updated to English; the confirmed mixed-language page copy was localized in both dictionaries and asserted in E2E.
open_risks: []
```
