# Database migrations

The database schema is created, upgraded, scripted and rolled back by `wms-migrate`, a separate executable
installed next to the server. Run it as a separate step with an account that may change the schema (the DBA's
rights); the server's own service account never needs those rights.

The server checks the schema every time it starts. When a database is configured it refuses to start, and
says why, while the schema is behind its build (it names the pending migrations), ahead of its build (it names
the migrations it does not know), or the database cannot be reached.

## Two ways to apply migrations

| Mode | Setting | Who changes the schema |
|------|---------|------------------------|
| Separate step (default) | `Wms:Database:AutoMigrate` off | You run `wms-migrate` before starting the upgraded server. |
| Bundled install | `Wms:Database:AutoMigrate=true` | The server applies pending migrations when it starts. Use it only when the server process is the only thing that uses its own database. |

With either mode the server never downgrades a schema and never touches a schema that a newer version has
migrated.

## Connecting

`wms-migrate` reads the same `Wms:Database` settings as the server, from `appsettings.json` in the working
directory and from `Wms__Database__*` environment variables. Flags override them:

| Flag | Overrides |
|------|-----------|
| `--provider <SqlServer\|PostgreSql>` | `Wms:Database:Provider` |
| `--connection-string <cs>` | `Wms:Database:ConnectionString` |
| `--trust-server-certificate` | `Wms:Database:TrustServerCertificate` (SQL Server only) |

The account needs the right to create schemas in the database; no server-level right is needed. On an empty
database the first run creates the schema `wms` and the migrations history table `wms.migrations_history`.

## Commands

You name the version you want; the tool works out whether that is an upgrade or a downgrade and says which.

| Command | Does |
|---------|------|
| `wms-migrate` | Applies every pending migration, one at a time. |
| `wms-migrate --status` | Lists applied and pending migrations, the version this build expects, whether the database can be reached, and any migrations this build does not know. |
| `wms-migrate --to <migration>` | Moves the schema up or down to a migration: its full id (`20260920025830_Initial`), its name (`Initial`), the start of its id, or `0` for an empty schema. A name or partial id that matches more than one migration is refused; give the full id. |
| `wms-migrate --script` | Writes SQL for a DBA to review and run, instead of changing the database. Needs only `--provider`: no connection and no connection string. Add `--from` and `--to` for the change between two versions, and `--output <file>` to write a file instead of the screen. The script can be run more than once safely. |

### Downgrades that lose data

A downgrade that drops tables, columns or schemas, deletes rows, or runs custom SQL is refused until you add
`--confirm-data-loss`; the tool lists each of those steps first. The same applies to a downgrade script, which
also lists the steps at its top as `-- DATA LOSS` comments.

Restoring the backup taken before the upgrade is the preferred way back. A downgrade reverts the schema, not
data that a migration changed.

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | Done, or nothing to do. |
| `1` | A migration failed; the output names it. Migrations before it were applied. |
| `2` | Usage or configuration error, such as an unknown flag, a missing setting or a malformed connection string. |
| `3` | The downgrade loses data and needs `--confirm-data-loss`. Nothing was changed or written. |
| `4` | Nothing was applied: the database cannot be reached, or its schema is newer than this version. Upgrade the software, or restore the backup taken before the upgrade. |

## Examples

```bash
wms-migrate --status
wms-migrate
wms-migrate --script --provider SqlServer --output upgrade.sql
wms-migrate --to Initial --confirm-data-loss
```
