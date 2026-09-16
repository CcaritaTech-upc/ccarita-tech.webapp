# Forms and business-rule validation

Form testing must cover more than required fields and syntax. A valid-looking form can still violate relationships, policies, state, authorization, or domain constraints.

## Rule categories

| Category | Examples |
|----------|----------|
| **Field shape** | Required value, email syntax, length, numeric range |
| **Cross-field relationship** | Password confirmation must match; new password must differ from current password; start date must precede end date |
| **Business-context rule** | Minimum age depends on product, jurisdiction, account type, or consent policy |
| **State-dependent rule** | Email can be changed only while verification is pending |
| **Uniqueness/existence** | Email must be unique; referenced project must exist |
| **Authorization rule** | A builder cannot submit owner-only fields or assign privileged roles |
| **Temporal rule** | Invitation has not expired; subscription date is within the allowed period |
| **Robustness/security** | Oversized input, Unicode, normalization, encoded markup, omitted fields, direct API bypass |

“Inputs must not be equal” is not a universal validation. It becomes a testable rule only when the business meaning requires inequality, such as current and new passwords or distinct source and destination accounts.

## Source-of-truth rule

```text
Frontend validation = immediate guidance and interaction quality
Backend validation  = authoritative business enforcement
Database constraint = final integrity guarantee where applicable
```

The same business intent may appear at several boundaries, but each assertion differs:

- frontend: the user receives timely, accessible feedback;
- API: bypassing the frontend still fails safely;
- domain/application: the rule is expressed consistently;
- database: invalid concurrent state cannot be persisted.

## Test allocation

| Technique | Best use |
|-----------|----------|
| Unit/equivalence partitions | Many formats, boundaries, and pure cross-field combinations |
| Component | Feedback timing, disabled actions, focus, labels, and error association |
| Frontend integration | Async validation, API errors, store and router effects |
| API/application | Authoritative rejection, normalization, authorization, business context |
| Persistence | Concurrent uniqueness and relational integrity |
| E2E | Representative user-critical validation and recovery path |

## Contextual age example

A generic `age >= 18` validator is usually the wrong abstraction. Prefer a policy with explicit inputs:

```text
EligibilityPolicy(dateOfBirth, product, jurisdiction, evaluationDate)
```

Test:

- boundaries immediately before, on, and after eligibility;
- leap-day birth dates;
- timezone/date interpretation;
- missing or contradictory context;
- frontend feedback;
- direct API bypass;
- policy changes without rewriting unrelated UI tests.

## Combination control

Do not multiply every field value by every browser and every E2E path. Use:

- equivalence classes;
- boundary-value analysis;
- decision tables for business combinations;
- pairwise testing for broad configuration sets;
- property-based testing for invariant-heavy inputs;
- one or two representative E2E cases per critical behavior.

The goal is meaningful behavioral coverage, not combinatorial exhaustion.
