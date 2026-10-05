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

- Device data reads require ownership: Builders see devices in their own
  projects; Owners see devices in units assigned through `UnitOwnerProjection`.
  Collection filters (`projectId`, `unitId`) narrow that authorized set; they
  never broaden it. Foreign device IDs read as not found.
- The device list, by-ID read, status, and energy routes share this access
  boundary. There is no global Admin list role; other roles cannot list devices.
- The device-type catalog and anonymous telemetry ingestion are separate
  endpoints with their own intentionally different access rules.
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
