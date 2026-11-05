# Configuration

These are the settings the installer or an operator sets outside the product; everything else is a setting in
the console's Configure workspace. Each key can be set in `appsettings.json`, in
`appsettings.<Environment>.json`, or as an environment variable with `__` in place of `:`
(`Wms__Database__Provider`). Environment variables win.

Only the keys that let the server start belong in these files: the ones on this page, plus the standard ASP.NET
Core hosting keys (`Urls`, `Kestrel`, `AllowedHosts`) and `Logging`. Any other key in an `appsettings` file is
ignored, and the server logs one warning at startup naming each such key and its file, so a value typed into
the wrong place is noticed. Those values are settings: change them in the Configure workspace instead.

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

## Keeping the connection string secret

| Key | Values | Notes |
|-----|--------|-------|
| `Wms:DataProtection:KeyRingPath` | A folder | Where the server keeps its encryption key ring. It is created on first run, readable only by the account the server runs as; back it up, and give every server that shares an encrypted connection string the same folder (for containers, a mounted volume). |

To keep the database password out of plain text, encrypt the connection string once:

```
wms-migrate --protect --connection-string "<plain connection string>" --key-ring <folder>
```

It prints the string as `enc:v1:…`; put that in `Wms:Database:ConnectionString` (or the
`Wms__Database__ConnectionString` environment variable) instead of the plain text. An encrypted connection
string needs `KeyRingPath`: if the folder is missing or does not hold the key, the server stops at startup
and says so. A plain connection string still works, for development.

Supported databases: SQL Server 2022 and later, including Express, and PostgreSQL 16 and later. Creating the
database and applying its migrations is a separate step outside the API, with `wms-migrate` and the database
administrator's rights; the server's own account never changes the schema. See
[Database migrations](migrate.md) and [Before the API can serve](bootstrap.md).
