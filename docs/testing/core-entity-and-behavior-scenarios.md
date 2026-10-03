# Core entity tests and behavior scenarios

This document maps the coursework's Core Entities Unit Tests and Core Behavior-
Driven Development items to executable tests. Scenarios use Given/When/Then
language in test names and this index; they are not Gherkin files and do not use
a BDD runner. `[Trait("Category", "BDD")]` makes the scenario-oriented tests
filterable without claiming a Gherkin toolchain or a test-first history.

## Core entity unit tests

All tests below run directly against domain entities with no database, HTTP,
service, or external dependency (`Layer=Domain`, `Dependency=None`).

| Context / entity | Scenario test | Observable outcome |
|---|---|---|
| Publishing / Unit | `UnitEntityTests.Given_a_unit_without_a_room_number_when_created_then_unit_number_is_used_as_room_number` | Room defaults to unit number and status defaults to available. |
| Publishing / Unit | `UnitEntityTests.Given_an_available_unit_when_an_owner_is_assigned_then_unit_becomes_occupied` | Owner email/id are set and status becomes occupied. |
| Publishing / Unit | `UnitEntityTests.Given_an_occupied_unit_when_owner_email_is_cleared_then_unit_becomes_available` | Null, empty, and whitespace emails clear owner fields and restore available status. |
| Publishing / Client | `ClientEntityTests.Given_client_details_when_client_is_created_then_details_and_default_status_are_kept` | Constructor stores client/project/owner details and leaves unit association unset. |
| Publishing / Client | `ClientEntityTests.Given_an_existing_client_when_details_and_unit_are_updated_then_new_values_replace_old_values` | Update replaces profile and unit-association values. |
| Subscriptions / Plan | `PlanEntityTests.Given_a_plan_name_description_and_price_when_created_then_monthly_interval_and_empty_features_are_defaulted` | Constructor stores supplied values and defaults interval/features. |
| Subscriptions / Plan | `PlanEntityTests.Given_an_existing_plan_when_plan_details_are_updated_then_new_values_are_kept` | Update replaces all plan fields. |

## Behavior-driven scenarios

The same test methods above are the executable evidence for these concise
business-facing scenarios:

1. **Unit availability:** Given an available unit, when an owner is assigned,
   then it is occupied; when the owner email is removed or blank, then owner
   identity is cleared and the unit is available again.
2. **Client assignment:** Given an existing client, when their profile and unit
   association are updated, then all new values are reflected by the entity.
3. **Subscription plan maintenance:** Given a plan, when its commercial details
   are updated, then name, description, price, billing interval, and features
   reflect the new values.

These scenarios specify and verify the entity-level behavior covered here; they
do not imply that every system journey was developed with a formal BDD process.

## Run

```sh
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "Layer=Domain"
dotnet test backend/tests/Modules/IoBuild.Modules.Tests.csproj --no-restore --filter "Category=BDD"
```
