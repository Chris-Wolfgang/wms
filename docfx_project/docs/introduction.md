# Introduction

Wolfgang.Wms is a warehouse management system built as a modular monolith: one API, one database and one deployable,
with the picking module first. It is an application you install (a database, the API, the web console and the
handheld app), not a library you reference. The package meant for others to reference is `Wolfgang.Wms.Client`,
the generated API client for devices and integrations.

## What is in it today

- **Two database engines.** SQL Server 2022 or later (Express included) and PostgreSQL 16 or later, from one model,
  with migrations generated per engine and a conventions check that proves both schemas match.
- **`wms-migrate`.** The one way the schema is created, upgraded, scripted or rolled back, run with the database
  administrator's rights. The API never changes the schema and refuses to start while the schema is behind or
  ahead of its build. See [Database migrations](migrate.md).
- **The console.** A Blazor Server console with five workspaces (Configure, Supervise, Resolve, Report, Insights),
  each a license feature and a permission. Screens are scan-first: a tethered scanner's input is taken anywhere,
  and the screen that owns it handles it. See [The console](console.md).
- **The handheld app.** An Android app for the floor, held to a minimum version the server publishes
  ([Device app version](device-app-version.md)).
- **The API.** Versioned under `/api/v0`, documented by a committed OpenAPI document, with keyset paging, ETags and
  `If-Match` for safe updates, idempotency keys, and problem details that carry stable error codes.
- **Your identifiers.** SKU codes, tote barcodes and the like are customer-supplied: formats, masks and GS1
  structure are validated before a label reaches a screen ([Your identifiers](identifiers.md)).

## Getting help

- Start with [Getting Started](getting-started.md).
- Configuration questions: [Configuration](configuration.md); first-run order: [Before the API can serve](bootstrap.md).
- The [API Reference](../api/index.md) is generated from the source.
- Report a problem on [GitHub Issues](https://github.com/Chris-Wolfgang/wms/issues).
