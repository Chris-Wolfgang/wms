# Configuration

Settings the installer or operator sets outside the product (everything else is a setting in the Configure
workspace, E6/E7). Each key can be set in `appsettings.json`, `appsettings.<Environment>.json`, or as an
environment variable with `__` for `:` (`Wms__Database__Provider`). Environment variables win.

## Only bootstrap keys (E6.5)

`appsettings` holds what the process needs before it can reach the database, and nothing else:

| Key | Story |
|-----|-------|
| `Wms:Database:Provider`, `ConnectionString`, `AutoMigrate`, `TrustServerCertificate` | E2, below |
| `Wms:DataProtection:KeyRingPath` | E8.1 (reserved until it lands) |
| `Wms:Bootstrap:AdminUserName` | E9.1 (reserved until it lands) |
| `Urls`, `Kestrel:*`, `AllowedHosts` | ASP.NET Core hosting |
| `Logging:*` | log levels; runtime control arrives with E12.4 |

Any other key in an `appsettings*.json` file is ignored, and the host logs one warning at startup naming each
one with its file (`Smtp:Host (appsettings.json)`), so a value typed into the wrong place is noticed. Those
values are settings: define them in a module (docs/SETTINGS.md) and edit them in the Configure workspace.
Environment variables are not inspected (the platform sets its own).

## Database (E2)

| Key | Values | Notes |
|-----|--------|-------|
| `Wms:Database:Provider` | `SqlServer`, `PostgreSql`, `None` | Chosen at install time. `None` starts the host without a database for bootstrap only (`GET /system/schema`, health); it is the shipped default so a fresh install can be probed before it is configured. Anything else fails startup: `Wms:Database:Provider must be one of SqlServer, PostgreSql or None; got 'Oracle'.` |
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
model is shared; migrations are generated per provider (E2.4) and applied by `wms migrate`, never by the API
(docs/BOOTSTRAP.md).
