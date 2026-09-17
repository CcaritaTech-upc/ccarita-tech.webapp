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
- Photo replacement is compare-and-swap on `PhotoReference`: a stale expected
  reference conflicts (409) instead of silently overwriting; a failed upload
  aborts without touching the stored photo.

## Photo transport

- Uploads go through `ICloudinaryUploader`; without Cloudinary configuration
  the upload returns null and the workflow aborts fail-closed. Tests inject a
  fake uploader, never the Cloudinary SDK or network.
