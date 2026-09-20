# Wolfgang.Wms Documentation

Wolfgang.Wms is a warehouse management system you install: a database, the API, the web console and the handheld
app, starting with the picking module. These pages are for the people who install, configure and operate it.

## Start here

- [Introduction](introduction.md) - what the product is, what is in it today and what is still arriving
- [Getting Started](getting-started.md) - from an empty database to a console you can open
- [Licensing](licensing.md) - tiers, keys, limits and expiry; the [feature comparison](licensing/feature-comparison.md) per release
- [Troubleshooting](troubleshooting.md) - every error code, what it means and what to do

## Operating the server

- [Before the API can serve](bootstrap.md) - the order of first-run steps and the schema endpoint
- [Configuration](configuration.md) - every `Wms:*` setting, where it is read from and what it does
- [Running in containers](containers.md) - the images, the worker roles, the compose stack and its variables
- [Signing in and identity providers](sign-in.md) - which sign-in methods the console offers, the lock-out override, and the provider endpoints
- [Database migrations](migrate.md) - `wms-migrate`: apply, status, script, targeted up and down
- [Integrity signatures](integrity.md) - how security rows are signed, what is guarded against, repair and alerting
- [Database conventions](database-conventions.md) - how tables, columns, keys and indexes are named on both engines

## Using the console and devices

- [The console](console.md) - workspaces, scanning, and what a user sees
- [Device app version](device-app-version.md) - how the server reads the handheld app's version today (every
  version is accepted; a server-published minimum is planned)
- [Your identifiers](identifiers.md) - identifier formats, masks and GS1 validation

## Reference

- [API Reference](../api/index.md) - generated from the source code
- Generated from the product: [settings](reference/settings.md), [permissions](reference/permissions.md), [error codes](reference/error-codes.md), [modules](reference/modules.md)
- [Project website](https://github.com/Chris-Wolfgang/wms)
