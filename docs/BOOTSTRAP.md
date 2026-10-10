# Bootstrap: what runs outside the API (E82.5)

Everything a customer does with the product goes through the one web API (E82.1), with these named
exceptions. Each exists because the API cannot do its normal work until it has happened, or because it must
work when the API cannot. Nothing else is allowed outside the API; a new exception is an ADR.

This is the engineering view. The operator-facing page is `docfx_project/docs/bootstrap.md` (published as
"Before the API can serve"); keep the two in step.

| Step | Why it is outside the API | Runs as | Defined by |
|------|---------------------------|---------|------------|
| Provision the database and apply migrations | With a database configured the API refuses to start until the schema matches its build (E4.4); migrations need elevated database rights the service account never has | the `wms-migrate` executable (in-process EF, JIT) run by the installer or an operator; the API never applies them itself | E2, E4, E82.6 |
| Configure the identity provider and the first administrator | Nobody can call the API before someone can authenticate | Installer / configuration, then Entra ID or the built-in store | E11 |
| Install the license file | Licensing gates every workspace and feature, so the first license is loaded before the first request | Installer or the console's Configure workspace on first run | E79 |
| TLS certificate and reverse proxy | Transport is configured around the process, not by it | Host / container configuration | E83 |
| Backup and restore | Must work when the API is down | `wms backup` / `wms restore` (CLI) | E65 |

## What the API says about bootstrap

`GET /api/v0/system/schema` (`Wolfgang.Wms.Core.Schema.SchemaModule`) is read-only. The API refuses to start
while a configured database is behind or ahead of its build (`SchemaStartupCheck`), so the two responses a
running API gives are a migrated database:

```json
{ "current": "20260920035312_RowVersionSequence", "expected": "20260920035312_RowVersionSequence", "upToDate": true }
```

and no database at all (`Wms:Database:Provider` = `None`, the not-installed source):

```json
{ "current": null, "expected": null, "upToDate": false }
```

`current` can still turn `null` with `expected` set when the database becomes unreachable after startup; the
reason is in the API log.

- `current`: the last applied migration, or `null` when no database is reachable or none was applied.
- `expected`: the last migration this build ships for the configured provider (the identifiers differ per
  provider), or `null` when no provider is configured.
- `upToDate`: true when a migration is applied and it is the one this build expects; never true while
  `current` is `null`.

Health checks read it once the API is up. Whether `wms-migrate` must run is decided before the API starts, from
`wms-migrate --status` ([MIGRATE.md](MIGRATE.md)); the API never applies a migration itself
([CONFIGURATION.md](CONFIGURATION.md)).
