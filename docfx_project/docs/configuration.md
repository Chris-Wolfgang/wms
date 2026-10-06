# Configuration

These are the settings the installer or an operator sets outside the product; everything else is a setting in
the console's Configure workspace. Each key can be set in `appsettings.json`, in
`appsettings.<Environment>.json`, or as an environment variable with `__` in place of `:`
(`Wms__Database__Provider`). Environment variables win.

## Database

| Key | Values | Notes |
|-----|--------|-------|
| `Wms:Database:Provider` | `SqlServer`, `PostgreSql`, `None` | Chosen at install time; names only, in any letter case. `None`, the shipped default, means no database is configured yet: the server still starts, so you can check that a fresh install runs before you set up a database (`GET /api/v0/system/schema` answers and reports no database), but nothing that needs data works. Set `SqlServer` or `PostgreSql` once the database exists. Any other value stops startup and names the accepted values; for example, setting it to `Oracle` gives `Wms:Database:Provider must be one of None, SqlServer or PostgreSql; got 'Oracle'.` |
| `Wms:Database:ConnectionString` | The database connection string | Required for `SqlServer` and `PostgreSql`; startup stops when it is missing. Keep passwords out of `appsettings.json`: use an environment variable or the secrets store. |
| `Wms:Database:TrustServerCertificate` | `true` or `false` (default) | SQL Server only. Trusts the server's certificate without checking who issued it, which SQL Server Express and self-signed test servers need; encryption is then always required (`Encrypt=Mandatory`, or `Strict` if you set it). Never use it on a shared network: install a proper certificate instead. Setting it with `PostgreSql` stops startup. |

Examples:

```json
{ "Wms": { "Database": { "Provider": "SqlServer", "ConnectionString": "Server=db;Database=wms;User Id=wms;Password=…;Encrypt=True", "TrustServerCertificate": true } } }
```

```json
{ "Wms": { "Database": { "Provider": "PostgreSql", "ConnectionString": "Host=db;Database=wms;Username=wms;Password=…" } } }
```

Supported databases: SQL Server 2022 and later, including Express, and PostgreSQL 16 and later. Creating the
database and applying its migrations happens outside the API; see [Before the API can serve](bootstrap.md).
