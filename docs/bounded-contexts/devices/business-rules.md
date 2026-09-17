# Devices business rules

Status: piloted. Rules verified against the codebase under Convergent Testing.
Journey: DEVICES.CONTROL (Owner sends commands to own unit devices).

## Identity

- Served actors: `Owner` controls unit devices; `Builder` provisions and
  manages project devices. The catalog fixes controllable attributes per type
  (power, brightness, mode, targetTemperature with ranges).
- A device belongs to a project and optionally to a unit; unit ownership is
  proven by `UnitOwnerProjection`, project ownership by `Project.BuilderId`.

## Manage

- Reading the device list requires login but is unfiltered by design for now
  (the frontend fetches all and filters client-side); per-role visibility
  scoping is a scheduled risk, not a silent guarantee.
- Mutations require ownership: the unit owner for unit devices, the project
  builder for project devices. Foreign ids read as not found.
- Custom unit devices require the Owner role plus propagated unit ownership;
  floor-scope catalog types cannot live in a unit; MAC addresses and
  per-unit type rows stay unique (409 on duplicates).
- Telemetry ingestion is anonymous by design (devices report without users).

## Control

- Only unit owners send commands: Owner role, device assigned to a unit, and
  matching unit ownership, enforced inside the service with per-device locks.
- Attributes validate per type and range before anything is persisted or
  published; violations fail closed with 400.
- Desired state persists in the device shadow before publish; unacknowledged
  commands republish; telemetry merges without regressing reported state.
