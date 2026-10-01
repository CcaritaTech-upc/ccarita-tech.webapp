# Profiles business rules

Status: piloted. Rules verified against the codebase under Convergent Testing.
Journey: PROFILES.MANAGE (Builder and Owner variants).

## Identity

- Served actors: `Builder` and `Owner`, each with their own view; the router
  selects by role and the store loads by the authenticated user id.
- A profile belongs to exactly one user (`Profile.UserId`, unique).

## Manage

- Reading is scoped to self: the list returns only the caller's profile, an
  explicit foreign filter is forbidden (403), and a foreign id reads as not
  found (no existence oracle).
- Updates are partial: name/username change only on non-blank values; contact
  fields apply as sent. Only the owner writes.
- Creation binds to the caller: a profile cannot be created for another user.
- One profile per user (unique `UserId` index): a duplicate create conflicts
  (409) instead of duplicating or exploding with a 500.
- Photo replacement is compare-and-swap on `PhotoReference`: a stale expected
  reference conflicts (409) instead of silently overwriting; a failed upload
  aborts without touching the stored photo.

## Role-specific profile facts

- `Age` is the person's age; it remains the Owner profile field.
- `YearsInBusiness` is a separate nullable integer for Builder profiles. New
  Builder registration requires a whole number from 0 through 120; zero is a
  valid value for a newly established business.
- The API rejects provided `YearsInBusiness` values outside 0–120. An omitted
  value in a profile update preserves the stored value.
- Existing Builder rows are upgraded additively: when the new nullable column
  is introduced, the migration runner copies an in-range legacy Builder `Age`
  value to `YearsInBusiness` while retaining the source value. Owner ages are
  not changed, and no legacy data is discarded by the schema upgrade.

## Photo transport

- Uploads go through `ICloudinaryUploader`; without Cloudinary configuration
  the upload returns null and the workflow aborts fail-closed. Tests inject a
  fake uploader, never the Cloudinary SDK or network.
