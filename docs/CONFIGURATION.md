# Configuration

Settings the installer or operator sets outside the product (everything else is a setting in the Configure
workspace, E6/E7). Each key can be set in `appsettings.json`, `appsettings.<Environment>.json`, or as an
environment variable with `__` for `:` (`Wms__Database__Provider`). Environment variables win.

The operator-facing page is `docfx_project/docs/configuration.md` (published as "Configuration"); keep the
two in step.

## Database (E2)

| Key | Values | Notes |
|-----|--------|-------|
| `Wms:Database:Provider` | `SqlServer`, `PostgreSql`, `None` | Chosen at install time. `None` starts the host without a database (only `GET /api/v0/system/schema` is useful; nothing that needs data works); it is the shipped default so a fresh install can be probed before it is configured. Anything else fails startup with the accepted names taken from `DatabaseProvider` (`DatabaseOptions.AcceptedProviders`): `Wms:Database:Provider must be one of None, SqlServer or PostgreSql; got 'Oracle'.` |
| `Wms:Database:ConnectionString` | provider connection string | Required for `SqlServer` and `PostgreSql`; startup fails when missing. Keep secrets out of `appsettings.json`: use an environment variable or the secrets store (E8). |
| `Wms:Database:AutoMigrate` | `true` / `false` (default) | Apply pending migrations when the API starts instead of refusing to start (E4.4). **Bundled installs only** (the installer's own database, one process): everywhere else run `wms-migrate` as a separate step with the DBA's rights and leave this off. |
| `Wms:Database:TrustServerCertificate` | `true` / `false` (default) | SQL Server only. Trusts the server certificate without validating its chain, which SQL Server Express and self-signed development servers need. Never on a shared network: install a certificate instead. Setting it with `PostgreSql` fails startup. |

Examples:

```json
{ "Wms": { "Database": { "Provider": "SqlServer", "ConnectionString": "Server=db;Database=wms;User Id=wms;Password=…;Encrypt=True", "TrustServerCertificate": true } } }
```

```json
{ "Wms": { "Database": { "Provider": "PostgreSql", "ConnectionString": "Host=db;Database=wms;Username=wms;Password=…" } } }
```

Supported engines: SQL Server 2022 and later including Express (E2.2), PostgreSQL 16 and later (E2.3). The
model is shared; migrations are generated per provider (E2.4). They are applied in one of two ways
([MIGRATE.md](MIGRATE.md), [BOOTSTRAP.md](BOOTSTRAP.md)):

- **Separate step (the default, `AutoMigrate` off):** an operator or installer runs the `wms-migrate`
  executable with the DBA's rights. The API never changes the schema; it refuses to start while the schema is
  behind, ahead of its build, or unreachable, naming the migrations.
- **Bundled installs (`AutoMigrate=true`):** the API applies pending migrations itself at startup, then starts.
  It still refuses to start when the database is unreachable or its schema is newer than the build, and it
  never downgrades.
