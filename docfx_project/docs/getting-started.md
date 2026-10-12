# Getting Started

From an empty database to a console you can open. Each step links to the page that goes into detail. Today the
server is built from source: no installer, container image or downloadable bundle has shipped yet (they arrive
with E14, E15 and before v1.0), so the commands below run the projects from a clone of the repository.

## Prerequisites

- A database server: SQL Server 2022 or later (Express included) or PostgreSQL 16 or later, with a login that may
  create the schema (the database administrator's) and one for the server that may only read and write data.
- The .NET 10 SDK and a clone of the repository, to build and run the API, the console and `wms-migrate`.
- Android devices for the floor, when the handheld app is in use.

## 1. Create the database and point the server at it

Create an empty database. Then set `Wms:Database:Provider` (`SqlServer` or `PostgreSql`) and
`Wms:Database:ConnectionString`. Keep the connection string out of `appsettings.json` and off every command line:
set it as the environment variable `Wms__Database__ConnectionString` or through your secrets store. See
[Configuration](configuration.md). Both `wms-migrate` and the API read these settings.

## 2. Apply the schema with `wms-migrate`

With the provider and the administrator's connection string in the environment:

```bash
export Wms__Database__Provider=SqlServer            # or PostgreSql; PowerShell: $env:Wms__Database__Provider = 'SqlServer'
export Wms__Database__ConnectionString="<administrator connection string>"
dotnet run --project src/Wolfgang.Wms.Migrate
dotnet run --project src/Wolfgang.Wms.Migrate -- --status
```

`--status` reports `Reachable: yes` and `Pending (0)` once the schema is in. The server's own account never runs
migrations, and the API refuses to start while the schema is behind or ahead of its build. See
[Database migrations](migrate.md).

## 3. Start the API and check it

Put the server's (data-only) connection string in `Wms__Database__ConnectionString`, start the API
(`dotnet run --project src/Wolfgang.Wms.Api`) and read `GET /api/v0/system/schema`: `upToDate` must be `true`.
The order of every first-run step, and what the endpoint reports before each, is in
[Before the API can serve](bootstrap.md).

## 4. Open the console

Start the console in another terminal (`dotnet run --project src/Wolfgang.Wms.Web`). There is no sign-in yet:
access is a placeholder that opens the free-tier workspaces to anyone. The root page lists the workspaces you can
enter, or takes you straight into it when there is exactly one; see [The console](console.md) for the workspaces
and how scanning works.

## Next steps

- [Your identifiers](identifiers.md) - decide the formats of your SKU codes, barcodes and other identifiers.
- [Device app version](device-app-version.md) - how the server handles the handheld app's version today; the
  minimum-version setting arrives with the settings module.
- [Database conventions](database-conventions.md) - for the DBA: how the schema is named and versioned.
- [API Reference](../api/index.md) - for integrations; the `Wolfgang.Wms.Client` package wraps it.
