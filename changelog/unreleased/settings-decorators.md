type: internal

Settings and session revocations are decorated rather than caching inline: `ProtectedSettings` (secrets encrypted at rest) wraps `CachedSettings` (resolutions memoised per instance until the settings or zones tables move) wraps the plain store, and `CachedSessionRevocations` memoises each user's "sessions valid after" so the per-request session check no longer queries the users table.
