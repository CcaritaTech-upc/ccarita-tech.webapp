# Subscriptions business rules

Status: piloted. Rules verified against the codebase under Convergent Testing.
Journey: SUBSCRIPTIONS.PURCHASE (Builder-only actor).

## Identity

- Served role: `Builder`. Plans are anonymous-readable; purchase and invoices
  require the purchasing builder's id throughout checkout, confirm, and invoices.
- Unknown roles are rejected at registration (IAM whitelist); subscriptions
  trusts the authenticated builder id, never a client-provided role.

## Purchase

- Plans list and detail are anonymous (`GET /api/v1/plans`, `GET /api/v1/plans/{id}`).
- Checkout creates a session (`POST /api/v1/subscriptions/payments/sessions` → 201
  with `checkoutUrl`); without a resolvable restricted key it fails closed (503).
- Confirming a paid session (`PATCH .../payments/sessions/{sessionId}` → 200)
  activates exactly one subscription per builder+plan; other active
  subscriptions of the builder expire with `EndDate` set (no overlaps).
- Cancel sets status to `cancelled` (`POST /api/v1/subscriptions/{id}/cancel`).
- Exactly one `active` subscription per builder, enforced by the
  `ActiveBuilderId` arbiter (generated column plus unique index); a concurrent
  confirm loser receives 409 and retries into the winner.
- Checkout requires an existing plan (unknown plan → 404) and a resolvable
  restricted key (none → 503); empty Stripe configuration never simulates.
- Malformed webhook bodies are rejected with 400, never 500.
- Invoices list per builder (`GET .../payments/invoices?builderId=`); when the
  provider has no live customer, they are synthesized from local subscriptions.

## Stripe transport (least privilege)

- Only restricted keys (`rk_`) reach Stripe on the wire, in either configured
  slot; secret keys (`sk_`) fail closed (null key → 503, invalid options throw).
- A configured secret key must never silently replace the restricted key on
  outgoing calls (regression covered at G0).

## Webhooks (out of journey scope, scheduled)

- Webhook processing is idempotent per event with signature verification, but
  no in-system flow calls it: only Stripe servers can, and simulated payments
  never fire. It is excluded from journey evidence until real Stripe keys and
  a public URL exist; the happy path activates via synchronous confirm.
