# Mandatory Convergent Testing Gate

Treat every new feature or behavior change as incomplete until it passes the project-local Convergent Testing Gate.

Before implementation or validation:

1. Load `.agents/skills/convergent-testing/SKILL.md`.
2. Define the System Journey and observable acceptance criteria.
3. Implement the behavior and its owned tests together.
4. Execute the relevant G0-G4 gates.
5. Load `convergent-failure-diagnosis` if any required gate fails or flakes.
6. Report deterministic evidence and unresolved risks before claiming completion.

Documentation-only changes may skip behavior gates with an explicit reason. MCP or interactive browser evidence is diagnostic only; final acceptance must come from versioned tests running without MCP.
