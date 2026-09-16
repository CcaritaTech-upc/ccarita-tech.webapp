# Devices business rules

Status: not-piloted. Rules below are a starting outline captured from the
existing codebase; they have not been verified under Convergent Testing.

- Only unit owners may send commands to devices assigned to their unit.
- Command attributes are validated per device type (power, brightness, mode, target temperature).
- Telemetry ingestion is durable before publish; unacknowledged commands are republished.
- Interrupted telemetry is repaired through a recovery intent before replay.
