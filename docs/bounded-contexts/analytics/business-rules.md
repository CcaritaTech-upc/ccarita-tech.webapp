# Analytics business rules

Status: piloted. Rules verified against the codebase under Convergent Testing.
Journey: ANALYTICS.VIEW (Builder dashboard and Owner dashboard variants).

## Identity

- Served actors: `Builder` and `Owner`, each with a dashboard endpoint family.
  Every metric route requires authentication and serves only the caller:
  foreign ids fail (404 on item routes, empty on scoped queries).
- Insights are project-scoped: the caller must own the project (builder) or
  own a unit inside it (owner); otherwise the project reads as not found.

## View

- Builder metrics aggregate projects, units, devices, and subscriptions.
- Owner metrics fall back to live energy data when projections are stale.
- Live energy windows clamp to 1–60 minutes (default 10).
- Device projections stay synchronized with provisioning events; dashboards
  read projections, never live device rows directly.
