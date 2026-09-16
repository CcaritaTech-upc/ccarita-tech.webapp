# IAM business rules

Authoritative rules for registration, authentication, authorized access,
logout, and token revocation. Backend enforcement is authoritative; frontend
proves feedback and interaction.

## Identity

- Email is trimmed and lowercased before comparison and storage.
- Email is required and at most 320 characters; violations fail closed.
- Password is required; sign-in with wrong or blank credentials creates no token.
- Served roles: `Builder`, `Owner`. Unknown roles are rejected.

## Registration

- Duplicate registration is idempotent: no second user, no duplicate side effects.
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
