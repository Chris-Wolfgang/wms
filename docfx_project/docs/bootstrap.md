# Before the API can serve

Everything you do with the product goes through its web API, with a few named exceptions. Each one exists
because the API cannot do its normal work until it has happened, or because it must work when the API cannot.

| Step | Why it happens outside the API | Who runs it |
|------|-------------------------------|-------------|
| Create the database and apply its migrations | Applying migrations needs database rights the server's own service account never has. With a database configured, the server refuses to start while its schema is behind or ahead of its version, and names the migrations | `wms-migrate`, run by the installer or an operator (see [Database migrations](migrate.md)); the server never applies them itself |
| Configure the identity provider and the first administrator | Nobody can call the API before someone can sign in | The installer and configuration, then Entra ID or the built-in user store |
| Install the license | Licensing decides which workspaces and features are available, so the first license is loaded before the first request | The installer, or the console's Configure workspace on first run |
| TLS certificate and reverse proxy | Transport is set up around the server process, not by it | Host or container configuration |
| Backup and restore | Must work when the API is down | Command-line tools |

## Checking the database schema

`GET /api/v0/system/schema` is read-only and answers whenever the server is running. The server does not start
while a configured database is behind or ahead of its version, so a running server answers either with a
migrated database:

```json
{ "current": "20260920035312_RowVersionSequence", "expected": "20260920035312_RowVersionSequence", "upToDate": true }
```

or with no database configured (`Wms:Database:Provider` set to `None`):

```json
{ "current": null, "expected": null, "upToDate": false }
```

| Field | Meaning |
|-------|---------|
| `current` | The last migration applied to the database, or `null` when no database is reachable or none was applied |
| `expected` | The last migration this version of the product ships for the configured provider (the identifiers differ per provider), or `null` when `Wms:Database:Provider` is `None` |
| `upToDate` | `true` when a migration is applied and it is the one this version expects; never `true` while `current` is `null`; always present |

Health checks read it once the server is up. Before the server is started, `wms-migrate --status` reports the
same information from the database itself, and that is where the decision to run `wms-migrate` is made.
