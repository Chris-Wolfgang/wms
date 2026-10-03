# wms-migrate (E4)

The one way the schema is created, upgraded, scripted or rolled back. It runs in-process with EF Core against
the provider in `Wms:Database` (or the flags), as a separate JIT executable next to the NativeAOT `wms` CLI
(`wms migrate …` forwards to it). The API never migrates unless `Wms:Database:AutoMigrate` is on, which is for
bundled installs only ([CONFIGURATION.md](CONFIGURATION.md)).

## Command surface

Declarative: you name a target, never a direction; the tool states the direction it took.

| Command | Does |
|---------|------|
| `wms-migrate` | apply every pending migration, one at a time |
| `wms-migrate --to <target>` | move up **or** down to `<target>`: a migration id (`20260920025830_Initial`), its name (`Initial`), its timestamp prefix, or `0` for an empty schema. Release versions resolve to that release's last migration once releases exist (the `release` skill records the map). |
| `wms-migrate --status` | applied and pending migrations, the version this build expects, and whether the database is ahead of the build |
| `wms-migrate --script [--from <m>] [--to <m>] [--output <file>]` | idempotent, provider-specific SQL for a DBA to review and run; needs **no** database connection; `--from` emits a delta; a downgrade script starts with a `-- Downgrade` header listing every data-losing step |
| `--confirm-data-loss` | required for an applied downgrade whose reverted migrations drop tables, columns, schemas or rows |
| `--provider`, `--connection-string`, `--trust-server-certificate` | override the configured `Wms:Database` values |

Exit codes: `0` ok, `1` a migration failed (the output names it), `2` usage or configuration error, `3` the
downgrade needs `--confirm-data-loss`.

## Rules

- **Empty database, `CREATE SCHEMA` rights** (E4.5): the first migration creates the module schemas and the
  history table `wms.migrations_history`; no server-level right is needed. Nothing lands in `dbo` or `public`.
- **Every migration has a working `Down`** (E4.6); CI runs up → down → up on both providers
  (`MigrateToolTests`) and `scripts/Check-Migrations.ps1` keeps both providers' migrations in step with the model.
- **The app refuses to start** (E4.4) when the database is behind (it names the pending migrations and points
  here), ahead (migrations this build does not know: upgrade the app or restore the backup), or unreachable.
- **Backup restore is the primary rollback.** `--to` downgrades exist for the case where a backup is not
  usable; they revert schema, not data that a migration transformed.

## Examples

```bash
wms-migrate --status
wms-migrate
wms-migrate --script --output upgrade.sql
wms-migrate --script --from 20260920025830_Initial --to AddZones
wms-migrate --to Initial --confirm-data-loss
```
