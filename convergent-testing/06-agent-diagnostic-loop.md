# Agent-assisted diagnostic loop

Agents accelerate investigation, but they do not redefine expected behavior. A failure is resolved only when a deterministic, versioned test proves the intended outcome from a clean environment.

## Diagnostic workflow

```text
Coded test fails
      ↓
Capture structured evidence
      ↓
Classify the failure
      ↓
Explore with controlled tools
      ↓
Propose and apply a scoped correction
      ↓
Run the coded test without MCP
      ↓
Run relevant lower and adjacent layers
      ↓
Record the regression and diagnostic learning
```

## Failure classification

The agent must distinguish:

| Class | Meaning |
|-------|---------|
| **Product defect** | Implemented behavior violates an accepted requirement |
| **Test defect** | Assertion, locator, fixture, or test assumption is incorrect |
| **Environment defect** | Service, dependency, configuration, or test data is unavailable or inconsistent |
| **Flaky behavior** | Timing, concurrency, order, or shared state makes the outcome nondeterministic |
| **Specification gap** | Intended behavior is ambiguous or contradictory |
| **Unknown** | Evidence is insufficient; investigation must not guess |

An agent must not weaken an assertion or silently change the expected outcome merely to obtain a green run.

## Playwright MCP role

Playwright MCP is an exploratory debugger. It may:

- inspect accessibility snapshots;
- reproduce user actions;
- inspect console and network failures;
- test candidate locators;
- capture screenshots;
- correlate visible symptoms with requests;
- validate a proposed interaction before coding it.

The MCP session is not the final test because it may contain hidden state, manual ordering, or unversioned decisions.

## Deterministic acceptance

After exploration:

1. Encode or update the Playwright test.
2. Start from documented data and authentication state.
3. Run through the normal Playwright test runner without MCP.
4. Run from a clean browser context.
5. Repeat suspected flaky paths enough times to expose nondeterminism.
6. Run affected component, contract, API, and persistence tests.
7. Preserve failure artifacts when the gate does not pass.

## Backend and database diagnosis

Browser evidence is insufficient for backend or persistence failures. Correlate the browser action with:

- request and trace identifiers;
- backend structured logs;
- dependency and container health;
- sanitized SQL error codes;
- migration version;
- transaction outcome;
- sanitized before/after state when explicitly permitted.

Do not expose secrets, raw credentials, tokens, or personal data to the agent. Diagnostic adapters should be read-only by default and limited to the test environment.

## Escalation rule

Human confirmation is required when the agent encounters an ambiguous product expectation, security policy, destructive data operation, or architecture decision. Mechanical reproduction and evidence collection may remain autonomous.
