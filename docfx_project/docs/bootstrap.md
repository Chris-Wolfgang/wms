# Before the API can serve

Everything you do with the product goes through its web API, with a few named exceptions. Each one exists
because the API cannot do its normal work until it has happened, or because it must work when the API cannot.

| Step | Why it happens outside the API | Who runs it |
|------|-------------------------------|-------------|
| Create the database and apply its migrations | The API starts without a schema and reports its state, but its data endpoints need the schema; applying migrations needs database rights the service account never has | The installer, or an operator with the migration tool (E4) |
| Configure the identity provider and the first administrator | Nobody can call the API before someone can sign in | The installer and configuration, then Entra ID or the built-in user store |
| Install the license | Licensing decides which workspaces and features are available, so the first license is loaded before the first request | The installer, or the console's Configure workspace on first run |
| TLS certificate and reverse proxy | Transport is set up around the server process, not by it | Host or container configuration |
| Backup and restore | Must work when the API is down | Command-line tools |

## Checking the database schema

`GET /api/v0/system/schema` is read-only and answers even when the database has never been migrated:

```json
{ "current": null, "expected": "20260919120000_Initial", "upToDate": false }
```

| Field | Meaning |
|-------|---------|
| `current` | The last migration applied to the database, or `null` when no database is reachable or none was applied |
| `expected` | The last migration this version of the product ships, or `null` before the data model exists |
| `upToDate` | `true` when the two are the same; always present |

Installers and health checks read it to decide whether migrations still need to be applied.
