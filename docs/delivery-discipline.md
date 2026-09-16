# Delivery discipline

Project-level delivery rules. This file is process, not theory: the testing
theory lives in `convergent-testing/`, the portable agent protocol lives in
`.agents/skills/`. No product behavior is defined here; product behavior lives
in `docs/bounded-contexts/<context>/business-rules.md`.

## 1. Evidence ledger per feature

Every behavior change closes with its `evidence-ledger.md` next to the
affected bounded context. Gate table uses `passed | failed | skipped`, and
every `skipped` gate requires a written reason. No ledger means not complete.

## 2. Risks have owners or they do not exist

Every `open_risk` carries an owner and a review date. An ownerless risk is
unmanaged and blocks completion. Use the template in
`convergent-testing/templates/evidence-ledger-template.md`.

## 3. WIP limit across tiers

Do not open the next risk tier while the previous one is red or has
unrecorded gaps. Close it or declare the debt first, then move on.

## 4. Deferral format

Everything postponed is written with four fields: what, why, who resumes it,
when. "We'll see later" without these four fields is not a deferral.

## 5. CI enforces, not witnesses

Anything that does not run in CI counts as `skipped`, never as `passed`.
Local-only evidence (E2E on a dev machine, opt-in database proofs) stays
yellow with a reason until CI runs it.

## 6. Where everything lives

| Content | Location |
|---|---|
| Portable agent protocol (agnostic) | `.agents/skills/` |
| Testing theory (agnostic) | `convergent-testing/` |
| This discipline (project process) | `docs/delivery-discipline.md` |
| Business rules per bounded context | `docs/bounded-contexts/<context>/business-rules.md` |
| Evidence ledgers per bounded context | `docs/bounded-contexts/<context>/evidence-ledger.md` |

Product content must never leak into skills or `AGENTS.md`.

## 7. Transversal coverage across roles

When a journey serves more than one role, proving it for a single role does
not prove the journey. Each role needs its own happy-path evidence at the
highest applicable layer; lower layers may share assertions only where the
code path is genuinely identical.

Concrete instance: `login` and `register` serve `Builder` and `Owner`.
Covering them for `Owner` alone left `Builder` unproven. From now on, a
transversal feature is complete only when every served role has its own
happy-path evidence.
