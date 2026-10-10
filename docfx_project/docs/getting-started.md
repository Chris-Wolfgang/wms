# Getting Started

From an empty database to a console you can open. Each step links to the page that goes into detail.

## Prerequisites

- A database server: SQL Server 2022 or later (Express included) or PostgreSQL 16 or later, with a login that may
  create the schema (the database administrator's) and one for the server that may only read and write data.
- The .NET 10 runtime on the server for the API, the console and `wms-migrate` (the Windows and container installs
  bring it with them).
- Android devices for the floor, when the handheld app is in use.

## 1. Create the database and point the server at it

Create an empty database. Then set `Wms:Database:Provider` (`SqlServer` or `PostgreSql`) and
`Wms:Database:ConnectionString` in `appsettings.json` or the environment; the connection string may be stored
encrypted. See [Configuration](configuration.md).

## 2. Apply the schema with `wms-migrate`

Run the tool with the administrator's connection string:

```bash
wms-migrate --provider SqlServer --connection-string "<connection string>"
wms-migrate --status --provider SqlServer --connection-string "<connection string>"
```

`--status` reports `Reachable: yes` and `Pending (0)` once the schema is in. The server's own account never runs
migrations, and the API refuses to start while the schema is behind or ahead of its build. See
[Database migrations](migrate.md).

## 3. Start the API and check it

Start the API with the server's (data-only) connection string and read `GET /api/v0/system/schema`: `upToDate`
must be `true`. The order of every first-run step, and what the endpoint reports before each, is in
[Before the API can serve](bootstrap.md).

## 4. Open the console

Start the console. Its root page lists the workspaces the signed-in user may enter; see [The console](console.md)
for the workspaces and how scanning works.

## Next steps

- [Your identifiers](identifiers.md) - decide the formats of your SKU codes, barcodes and other identifiers.
- [Device app version](device-app-version.md) - set the minimum handheld version before devices connect.
- [Database conventions](database-conventions.md) - for the DBA: how the schema is named and versioned.
- [API Reference](../api/index.md) - for integrations; the `Wolfgang.Wms.Client` package wraps it.
