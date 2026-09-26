# Bootstrap: what runs outside the API (E82.5)

Everything a customer does with the product goes through the one web API (E82.1), with these named
exceptions. Each exists because the API cannot serve until it has happened, or because it must work when
the API cannot. Nothing else is allowed outside the API; a new exception is an ADR.

| Step | Why it is outside the API | Runs as | Defined by |
|------|---------------------------|---------|------------|
| Provision the database and apply migrations | The API needs the schema before it can start; migrations need elevated database rights the service account never has | `wms migrate` (in-process EF, JIT executable) run by the installer or an operator | E2, E82.6 |
| Configure the identity provider and the first administrator | Nobody can call the API before someone can authenticate | Installer / configuration, then Entra ID or the built-in store | E11 |
| Install the license file | Licensing gates every workspace and feature, so the first license is loaded before the first request | Installer or the console's Configure workspace on first run | E79 |
| TLS certificate and reverse proxy | Transport is configured around the process, not by it | Host / container configuration | E83 |
| Backup and restore | Must work when the API is down | `wms backup` / `wms restore` (CLI) | E65 |

## First sign-in (E9.1)

Once the schema is installed, the first start of the API creates the bootstrap administrator
(`Wms:Bootstrap:AdminUserName`, default `admin`) with the documented default password
(docs/AUTH.md) and flags it "must change password": until it is replaced, every request but the password
change and sign-out answers `403 auth.password_change_required`. The default is refused as a new password.
Once any local administrator exists the bootstrap values are ignored.

## What the API says about bootstrap

`GET /api/v0/system/schema` (`Wolfgang.Wms.Core.Schema.SchemaModule`) is read-only and answers even when the
database has never been migrated:

```json
{ "current": null, "expected": "20260919120000_Initial", "upToDate": false }
```

- `current`: the last applied migration, or `null` when no database is reachable or none was applied.
- `expected`: the last migration this build ships, or `null` before the data model exists.
- `upToDate`: true when they are equal.

Installers and health checks read it to decide whether to run `wms migrate`; the API never applies a
migration itself. Until the data model (E2) exists the placeholder source reports both identifiers as `null`.
