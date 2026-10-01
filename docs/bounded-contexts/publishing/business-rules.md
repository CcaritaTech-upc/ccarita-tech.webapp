# Publishing business rules

Status: piloted. Rules verified against the codebase under Convergent Testing.
Journey: PUBLISHING.MANAGE (Builder creates projects, defines structure once,
manages units and clients).

## Identity

- Served actor: `Builder`. Projects carry name, description, location, and
  builder ownership; the project list is scoped to the caller.
- Units belong to a project; clients belong to a builder and optionally to a
  unit; a client email may invite a future owner (IAM auto-links on register).

## Manage

- Project reads and mutations require ownership: foreign ids read as not
  found; creation binds to the caller unless the body names them explicitly
  (mismatch is forbidden).
- Structure definition is one-shot per project (409 on redefine), requires
  the Builder role, and requires owning the project; it provisions units and
  floor devices deterministically.
- Unit creation and owner assignment require owning the parent project.
- Client reads and mutations require owning the client record; creation binds
  the builder id to the caller.
- Client listing is Builder-only and always scoped to the caller's `BuilderId`;
  an optional project filter must refer to the caller's project. Explicit
  `builderId` filters cannot select another builder.
- Client creation and update require an owned project; an assigned unit must
  belong to that same project. Submitted builder ownership is derived from the
  authenticated Builder, not trusted from the request body.
- All client REST endpoints require the `Builder` role; item reads and
  mutations additionally require ownership of the client record.
- Unfiltered unit listing remains a scheduled visibility risk; client-list
  isolation does not imply unit-list isolation.

## Structure

- Defining a project structure provisions units and floor devices atomically;
  floors and units-per-floor must be at least 1, floor references in range.
