# Vision and principles

Convergent Testing organizes quality around user outcomes rather than tools or repository folders. Each outcome is verified through focused evidence that progressively converges into a trustworthy system-level proof.

## Problem

Traditional suites are commonly grouped by technology:

```text
unit/
integration/
e2e/
```

That structure identifies how a test runs, but often hides:

- which user outcome it protects;
- whether frontend and backend assumptions agree;
- which risks remain untested;
- why the same assertion appears at multiple levels;
- what evidence an agent needs to diagnose a failure.

The result can be a large suite with weak product confidence.

## Desired outcome

For every critical System Journey, a team should be able to answer:

1. What user-visible result are we protecting?
2. Which business rules define success and failure?
3. Which layer owns each assertion?
4. Which boundaries have been verified?
5. Which risk scenarios are covered?
6. What evidence is available when something fails?
7. Can the final proof run deterministically without an agent?

## Principles

### 1. Journeys before tools

Start with behavior and outcomes. Select Vitest, xUnit, Playwright, containers, or another tool only after the required evidence is understood.

### 2. Cheapest trustworthy proof

Test each risk at the fastest and most specific layer capable of proving it. Do not use a browser to exhaustively test a pure validation function.

### 3. Progressive convergence

Verify internals first, then boundaries, then the complete system:

```text
specific behavior → component collaboration → boundary compatibility → system outcome
```

### 4. Backend authority for business rules

The frontend may mirror rules for immediate feedback. The backend remains authoritative because clients can be bypassed, outdated, or malicious.

### 5. Real infrastructure for infrastructure guarantees

Use doubles for fast orchestration tests. Use the production database engine and protocol-compatible dependencies when proving transactions, constraints, migrations, concurrency, or durability.

### 6. Risk determines depth

Frequency alone is insufficient. Rare security or data-loss failures may require the highest priority.

### 7. Exploration is not proof

Agents and MCP tools can investigate, reproduce, and propose changes. Acceptance requires versioned tests running from a clean, deterministic environment.

### 8. Failures improve the framework

Every unexplained failure is feedback about missing evidence, unclear ownership, insufficient observability, or an incomplete scenario model.

## Success criteria

The pilot is successful when it can:

- map one IoBuild IAM journey across frontend, API, application, and database layers;
- reveal meaningful gaps not visible from folder names or line coverage;
- prevent duplicated assertions across layers;
- diagnose a failed E2E from captured evidence;
- convert an exploratory reproduction into a deterministic regression test;
- remain understandable without requiring a specific language or framework.
