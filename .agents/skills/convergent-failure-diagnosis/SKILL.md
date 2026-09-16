---
name: convergent-failure-diagnosis
description: "Trigger: test failed, E2E failed, diagnose Playwright, flaky test. Runs bounded evidence-first hypothesis diagnosis and deterministic repair."
license: Apache-2.0
metadata:
  author: "ccarita-tech"
  version: "0.3"
---

## Activation Contract

Use when any Convergent Testing gate fails or passes only on retry.

## Hard Rules

- Preserve first-failure trace, logs, network, screenshot, and relevant database evidence.
- Never edit expectations or locators before classifying the failure.
- Never expose secrets or personal data to diagnostic tools.
- Limit repair loops to two; escalate unresolved ambiguity.
- Final acceptance runs from code without MCP.

## Decision Gates

Build an observable hypothesis graph with at most four branches: `product`, `test`, `environment`, `flaky/specification`. For each branch record evidence for, evidence against, and the cheapest discriminating check. Prune unsupported branches; do not narrate hidden chain-of-thought.

## Execution Steps

1. Reproduce once and capture evidence.
2. Build and prune the hypothesis graph.
3. Use Playwright MCP only if browser state or interaction remains ambiguous.
4. Apply the smallest behavior-preserving fix.
5. Run the original coded test from clean state.
6. Run adjacent lower-layer regressions.
7. Record verdict, root cause, and prevented regression.

## Output Contract

Return failure class, evidence graph, selected verdict, changed files, deterministic rerun result, and unresolved uncertainty.
