# Mandatory Convergent Testing Gate

Treat every new feature or behavior change as incomplete until it passes the project-local Convergent Testing Gate.

Before implementation or validation:

1. Load `.agents/skills/convergent-testing/SKILL.md`.
2. Load applicable project discipline and bounded-context records.
3. Define the System Journey, served actors, and observable acceptance criteria.
4. Implement the behavior and its owned tests together.
5. Execute the relevant G0-G4 gates.
6. Load `convergent-failure-diagnosis` if any required gate fails or flakes.
7. Report deterministic evidence, actor coverage, and owned unresolved risks before claiming completion.

Documentation-only changes may skip behavior gates with an explicit reason. MCP or interactive browser evidence is diagnostic only; final acceptance must come from versioned tests running without MCP.
