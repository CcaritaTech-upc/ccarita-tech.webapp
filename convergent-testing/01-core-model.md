# Core model

The framework uses a small set of concepts to connect product behavior, technical verification, risk, and diagnostic evidence.

## Concept map

| Concept | Definition |
|---------|------------|
| **System Journey** | A user- or actor-visible outcome that may cross UI, API, application, data, and external-service boundaries |
| **Scenario** | One path through a journey, including a happy path or a what-if condition |
| **Business rule** | A domain or contextual constraint that determines whether an action is allowed |
| **Verification layer** | The cheapest technical boundary capable of proving a specific assertion |
| **Convergence point** | A boundary where independently verified parts are tested together |
| **Risk profile** | Likelihood, impact, and detectability used to prioritize a scenario |
| **Evidence** | Structured artifacts used to prove success or diagnose failure |
| **Quality gate** | A deterministic condition required before promotion |
| **Diagnostic episode** | A traceable investigation that classifies and resolves a failure |

## Primary matrix

Every testable item should be locatable within three primary dimensions:

```text
System Journey × Verification Layer × Risk Scenario
```

Environment, dependency, browser, runtime, and execution speed are metadata. They should not redefine the behavioral scenario.

## Canonical scenario record

The documented form is technology-neutral. Future tooling may parse an equivalent YAML representation.

```yaml
journey: IAM.LOGIN
scenario: INVALID_CREDENTIALS
title: Reject an incorrect password without creating a session
kind: what-if
risk:
  tier: A
  likelihood: high
  impact: medium
  detectability: high
preconditions:
  - an active user exists
stimulus:
  - submit a valid email and an incorrect password
expected_outcomes:
  - the API rejects authentication
  - no session token is issued
  - the UI exposes a safe actionable message
layers:
  - application
  - api
  - frontend-integration
evidence:
  - test result
  - HTTP status and response schema
  - browser trace on failure
```

## Identifier convention

Use stable behavioral identifiers independent of file names:

```text
<DOMAIN>.<JOURNEY>.<SCENARIO>
```

Examples:

```text
IAM.REGISTRATION.HAPPY_PATH
IAM.REGISTRATION.DUPLICATE_EMAIL
IAM.LOGIN.INVALID_CREDENTIALS
IAM.LOGOUT.REVOKED_TOKEN_AFTER_RESTART
```

Test implementations should carry these identifiers through names, tags, traits, or annotations. This allows reports to aggregate one scenario across multiple technologies.

## Assertion ownership

Each assertion has one primary owner.

| Assertion | Primary owner |
|-----------|---------------|
| Email normalization rule | Domain or application |
| Form displays validation feedback | Component |
| Client sends the documented payload | Frontend integration or contract |
| Unauthorized response is `401` with the expected schema | API contract |
| Duplicate email is prevented concurrently | Database integration |
| User completes login and reaches the dashboard | System E2E |

Higher layers may sample a lower-level behavior to prove wiring, but should not exhaustively duplicate it.

## Completion rule

A journey is not complete merely because its E2E happy path passes. Completion requires:

- explicit acceptance criteria;
- assigned assertion ownership;
- required convergence points;
- risk-tier coverage appropriate to its criticality;
- deterministic quality gates;
- sufficient failure evidence.
