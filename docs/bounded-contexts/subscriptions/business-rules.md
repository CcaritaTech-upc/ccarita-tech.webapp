# Subscriptions business rules

Status: not-piloted. Rules below are a starting outline captured from the
existing codebase; they have not been verified under Convergent Testing.

- Stripe transport requires restricted keys (`rk_`); secret keys fail closed.
- Webhook processing is idempotent per event.
- Plan changes expire superseded plans instead of overlapping them.
