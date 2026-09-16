# `<DOMAIN>.<JOURNEY>` — `<Outcome>`

## Purpose

**Actor:** `<primary actor>`

**Trigger:** `<event that starts the journey>`

**Outcome:** `<observable user or system result>`

## Scope

### Included

- `<behavior or boundary>`

### Excluded

- `<explicit non-goal>`

## Preconditions

- `<required state>`

## Happy path

1. `<actor action or system transition>`
2. `<next step>`
3. `<observable outcome>`

## Postconditions

- `<durable state>`
- `<observable state>`

## Business rules

| Rule ID | Rule | Authority |
|---------|------|-----------|
| `<ID>` | `<constraint or policy>` | `<domain/application/database>` |

## Convergence map

| Layer or boundary | Evidence required | Tool or adapter |
|-------------------|-------------------|-----------------|
| Domain | `<proof>` | `<runner>` |
| Application | `<proof>` | `<runner>` |
| Component | `<proof>` | `<runner>` |
| Frontend + API | `<proof>` | `<contract mechanism>` |
| Backend + database | `<proof>` | `<real database fixture>` |
| System E2E | `<proof>` | `<browser runner>` |

## Scenarios

| Scenario ID | Kind | Tier | Primary layer | Expected outcome |
|-------------|------|------|---------------|------------------|
| `HAPPY_PATH` | Happy | Required | System/E2E | `<outcome>` |
| `<SCENARIO>` | What-if | A | `<layer>` | `<safe result>` |

## Dependencies and test data

- `<dependency>`
- `<fixture or builder>`
- `<isolation/reset strategy>`

## Required failure evidence

- `<trace, log, response, database error, or metric>`

## Quality gates

- [ ] Local layer evidence passes.
- [ ] Pairwise convergence passes.
- [ ] Happy-path system convergence passes.
- [ ] Required risk tiers pass.
- [ ] Diagnostic artifacts are available on failure.
