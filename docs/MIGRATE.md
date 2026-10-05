# wms-migrate (E4)

The one way the schema is created, upgraded, scripted or rolled back. `wms-migrate` is its own JIT executable
(`src/Wolfgang.Wms.Migrate`, assembly `wms-migrate`) that runs EF Core migrations in-process against the
provider in `Wms:Database` (or the flags). There is no `wms migrate` subcommand: the NativeAOT `wms` CLI does
not forward to it. A leading `migrate` word is accepted and ignored (`wms-migrate migrate --status`), so an
installer that passes the verb still works. The API never migrates unless `Wms:Database:AutoMigrate` is on,
which is for bundled installs only ([CONFIGURATION.md](CONFIGURATION.md)).

The operator-facing page is [docfx_project/docs/migrate.md](../docfx_project/docs/migrate.md) (published with
the documentation site); keep the two in step.

## Command surface

Declarative: you name a target, never a direction; the tool states the direction it took.

| Command | Does |
|---------|------|
| `wms-migrate` | apply every pending migration, one at a time |
| `wms-migrate --to <target>` | move up **or** down to `<target>`: a migration id (`20260920025830_Initial`), its name (`Initial`), its timestamp prefix, or `0` for an empty schema. A name or prefix that matches more than one migration is a usage error listing the matches; give the full id. Release versions resolve to that release's last migration once releases exist (the `release` skill records the map). |
| `wms-migrate --status` | applied and pending migrations, the version this build expects, whether the database is reachable (with the reason when not) and whether it is ahead of the build |
| `wms-migrate --script [--from <m>] [--to <m>] [--output <file>]` | idempotent, provider-specific SQL for a DBA to review and run; needs **no** database connection and no connection string (only `--provider`); `--from` emits a delta; a downgrade script starts with a `-- Downgrade` header listing every data-losing step |
| `--output <file>` | `--script` only; rejected without it |
| `--confirm-data-loss` | required for a downgrade, applied or scripted, whose reverted migrations drop tables, columns, schemas or rows, or run raw SQL (`MigrationBuilder.Sql`, which the tool does not inspect and so treats as data-losing) |
| `--provider`, `--connection-string`, `--trust-server-certificate` | override the configured `Wms:Database` values |

Exit codes: `0` ok, `1` a migration failed (the output names it), `2` usage or configuration error (including a
malformed connection string), `3` the downgrade (applied or scripted) needs `--confirm-data-loss`, `4` nothing
was applied because the database cannot be reached or its schema is newer than this build.

## Rules

- **Empty database, `CREATE SCHEMA` rights** (E4.5): EF creates schema `wms` together with the history table
  `wms.migrations_history`, before the first migration runs (`IF SCHEMA_ID(N'wms') IS NULL ... CREATE SCHEMA`
  on SQL Server, a `pg_namespace`-guarded `CREATE SCHEMA wms` on PostgreSQL; see any `--script` output). No
  server-level right is needed and nothing lands in `dbo` or `public`. A reachable database with no history
  table yet reports as reachable with nothing applied.
- **History location.** The history table is `wms.migrations_history`. EF's default `__EFMigrationsHistory` in
  `dbo`/`public` was used only by the unreleased E2.4 work (no release, tag or `main` commit carries it), whose
  only migration (`Initial`) has an empty `Up`, so no installation can have history there and there is no
  transfer step. Once a release has shipped, any change of location must ship a transfer.
- **Every migration has a working `Down`** (E4.6); CI runs up → down → up on both providers
  (`MigrateToolTests`) and `scripts/Check-Migrations.ps1` keeps both providers' migrations in step with the model.
- **A newer schema is never touched** (E4.6). When the history holds migrations this build does not ship, the
  tool applies nothing in either direction (exit `4`) and the app refuses to start, naming them: upgrade the
  app or restore the backup taken before the upgrade.
- **The app refuses to start** (E4.4) when the database is behind (it names the pending migrations and points
  here), ahead, or unreachable.
- **Backup restore is the primary rollback.** `--to` downgrades exist for the case where a backup is not
  usable; they revert schema, not data that a migration transformed.

## Examples

```bash
wms-migrate --status
wms-migrate
wms-migrate --script --provider SqlServer --output upgrade.sql
wms-migrate --script --provider PostgreSql --from 20260920025830_Initial --to AddZones
wms-migrate --to Initial --confirm-data-loss
```
