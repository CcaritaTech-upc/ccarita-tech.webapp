# Defining System Journeys

A System Journey describes an outcome, not a page sequence. It remains valid when the UI layout, endpoint implementation, or persistence mechanism changes.

## Journey boundary

A useful journey variant has:

- one primary actor;
- one meaningful goal;
- a clear trigger;
- observable success and failure outcomes;
- explicit preconditions and postconditions;
- a bounded number of external dependencies.

## Actor coverage for transversal journeys

A capability may serve several actors while each journey variant retains one primary actor. Enumerate the served actors first, then define one happy-path variant per actor whose permissions, route, data, or outcome can differ.

| Actor | Trigger | Expected outcome | Highest-layer evidence |
|---|---|---|---|
| `<actor A>` | `<trigger>` | `<outcome>` | `<system/E2E evidence>` |
| `<actor B>` | `<trigger>` | `<outcome>` | `<system/E2E evidence>` |

Shared lower-layer assertions are acceptable only when the implementation path is genuinely identical. A passing variant for one actor does not prove another actor.

Good examples:

- a visitor creates an owner account;
- an existing user starts and ends an authenticated session;
- a builder creates a project and defines its structure;
- an owner pays for a subscription;
- a device reports telemetry and the owner sees the updated status.

Avoid implementation-shaped journeys such as “submit POST `/sessions`” or “render `login.vue`”. Those are steps or adapters, not user outcomes.

## Discovery sequence

1. Enumerate every actor served by the capability.
2. State the primary actor and desired outcome for each variant.
3. Write each happy path without technical implementation details.
4. Define observable acceptance criteria.
5. Identify business rules and state transitions.
6. Mark trust boundaries and external dependencies.
7. Add what-if scenarios from risk analysis.
8. Assign each assertion to a verification layer.
9. Define convergence points and required evidence.

## Happy path first

The happy path establishes the intended state transition:

```text
Given a valid precondition
When the actor completes the intended action
Then the expected outcome and postcondition are observable
```

What-if scenarios should branch from a known state or transition. They should not become an unstructured list of edge cases.

## State model

Journeys should identify meaningful states and transitions, especially for authentication, payments, asynchronous work, and devices.

Example:

```text
Anonymous → Registered → Authenticated → Revoked
```

Scenarios can then target:

- rejected transitions;
- repeated transitions;
- interrupted transitions;
- concurrent transitions;
- recovery after partial failure;
- stale or expired state.

This structure supports future model-based scenario generation without requiring it during the documentation phase.

## Scenario propagation

A scenario belongs only in layers that provide distinct evidence.

For invalid email formats:

- exhaustive equivalence classes belong in domain or validation unit tests;
- representative feedback behavior belongs in component tests;
- API bypass belongs in an API test;
- at most one representative case belongs in E2E when the user-visible flow is critical.

The framework optimizes for confidence per maintenance cost, not maximum test count.
