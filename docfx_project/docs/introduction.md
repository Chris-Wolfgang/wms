# Introduction

Wolfgang.Wms is a warehouse management system built as a modular monolith: one API and one database, with business
modules living inside the API and the worker rather than being deployed on their own, and the picking module first.
It is an application you install (a database, the API, the web console and the handheld app), not a library you
reference. The package meant for others to reference is `Wolfgang.Wms.Client`, the generated API client for devices
and integrations.

## What is in it today

`main` holds the foundation the modules are built on. What is there now:

- **Two database engines.** SQL Server 2022 or later (Express included) and PostgreSQL 16 or later, from one model,
  with migrations generated per engine and a conventions check that proves both schemas match.
- **`wms-migrate`.** The one way the schema is created, upgraded, scripted or rolled back, run with the database
  administrator's rights. The API never changes the schema and refuses to start while the schema is behind or
  ahead of its build. See [Database migrations](migrate.md). It is built from source today; no installer or
  container image has shipped yet.
- **The console.** A Blazor Server console with five workspace shells (Configure, Supervise, Resolve, Report,
  Insights) and a scan listener that takes a tethered scanner's input on every screen, the screen that owns it
  handling it. Access is a placeholder that opens the free-tier workspaces to anyone; there is no sign-in yet.
  See [The console](console.md).
- **The handheld app.** An Android app with a landing page. It reports its version to the server, which today
  accepts every version; see [Device app version](device-app-version.md).
- **The API.** Versioned under `/api/v0` and documented by a committed OpenAPI document. Today it serves the root
  and the schema-status endpoint; the conventions the module endpoints will follow (keyset paging, ETags with
  `If-Match`, idempotency keys, problem details with stable error codes) are implemented as primitives and
  documented, with no module endpoint using them yet.
- **Your identifiers.** The rules for customer-supplied identifiers (formats, masks, GS1 structure) exist as domain
  code and are documented in [Your identifiers](identifiers.md); the intake endpoints and screens that will apply
  them to a label arrive with the modules.

## What is still arriving

The picking module and its screens, license-backed and permission-backed access with sign-in (E79, E11), the
server-published minimum handheld version (E12), the worker image and the Windows install (E14, E15), and a
downloadable bundle before v1.0. Each page above says what its topic looks like today.

## Getting help

- Start with [Getting Started](getting-started.md).
- Configuration questions: [Configuration](configuration.md); first-run order: [Before the API can serve](bootstrap.md).
- The [API Reference](../api/index.md) is generated from the source.
- Report a problem on [GitHub Issues](https://github.com/Chris-Wolfgang/wms/issues).
