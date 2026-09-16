# Analytics business rules

Status: not-piloted. Rules below are a starting outline captured from the
existing codebase; they have not been verified under Convergent Testing.

- Dashboards serve role-scoped data (`Builder` vs `Owner`).
- Owner metrics fall back to live energy data when projections are stale.
- Device projections stay synchronized with provisioning events.
