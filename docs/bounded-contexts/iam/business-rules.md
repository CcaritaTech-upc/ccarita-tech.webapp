# IAM business rules

Authoritative rules for registration, authentication, authorized access,
logout, and token revocation. Backend enforcement is authoritative; frontend
proves feedback and interaction.

## Identity

- Email is trimmed and lowercased before comparison and storage.
- Email is required and at most 320 characters; violations fail closed.
- Password is required and at least 8 characters at registration; sign-in with wrong or blank credentials creates no token.
- Registration emails must pass the frontend email syntax rule on the backend as well.
- Served roles: `Builder`, `Owner`. Unknown roles are rejected.

## Registration

- Duplicate registration is idempotent: no second user, no duplicate side effects.
- Owner registration requires an existing unit assigned to the normalized email, either through `Unit.OwnerEmail` or a matching client with a valid `UnitId`; a client record without a unit is not sufficient.
- The backend enforces Owner unit assignment before creating the account; the frontend blocks progression and disables the gray Next button when assignment is absent or cannot be verified.
- Builder registration does not require an assigned unit.
- The public `/users` directory requires the `Admin` role; ordinary `Builder` and `Owner` accounts cannot enumerate users. Admin provisioning is outside public registration.
- Anonymous invitation lookup reveals only assignment/account-status booleans, not the owner's name, contact, project, or unit details.
- A registration failure rolls back all durable state.
- Password confirmation must match (frontend feedback; exact, case-sensitive).
- A new password must differ from the current one (frontend feedback).

## Session

- Registration signs the user in and leaves the registration route.
- Login from a clean session succeeds with valid credentials.
- Protected requests require a valid, non-revoked bearer token.
- Logout revokes the token; the same token is rejected afterwards.
- A repeated logout after revocation is rejected without handler execution.
- Expired revocations are not treated as revoked.
