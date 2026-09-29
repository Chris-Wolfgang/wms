# API versioning (E82.2)

One web API, versioned by path segment: `/api/v{n}/…` (`WmsApi.RouteTemplate`). The version is never a header
or a query parameter, so a URL identifies a contract and can be shared, logged and cached as such. Each served
version has its own OpenAPI document at `/openapi/v{n}.json`; the copy committed under `docs/api/` is the
document the host serves (`OpenApiDocumentTests` fails when they drift; regenerate with `WMS_UPDATE_OPENAPI=1`).

## v0: unstable through product 0.x

`v0` is the only version while the product is below 1.0. It is documented as unstable: breaking changes ship
**in place**, each with a `breaking` changelog fragment that names the contract it breaks (HTTP API, binary,
or both). Integrators building on `v0` accept that and read the changelog per release.

## v1: frozen at product 1.0

At 1.0 the contract is copied to `v1` and `v1` joins `WmsApi.Frozen`. From then on:

- `v1` changes only **additively**: new optional fields, new endpoints, new enum values, new error codes.
  Clients must ignore fields, values and codes they do not know; that is part of the contract, not a courtesy.
- A breaking change is a new version (`v2`), never an edit to `v1`.
- `v0` is served as an **alias of `v1`** for a defined window (at least two minors or six months, whichever is
  longer) with `Deprecation` and `Sunset` headers on every response naming the date. Removing `v0` after the
  window is a product major.
- Removing any frozen version is a product major.

## What counts as breaking

Removing or renaming a path, method, field, enum value or error code; tightening validation; changing a
status code; changing the meaning or type of a field; making an optional input required; changing pagination
or idempotency semantics (E82.3). Adding any of those is additive.

## Enforcement

- `WmsApi.Served` / `WmsApi.Frozen` are the single source of the version list (`Wolfgang.Wms.Core.Api`).
- The CI OpenAPI diff compares the PR's `docs/api/openapi-v{n}.json` with `main`'s and fails a PR that breaks a
  version listed as frozen; for unstable versions it only reports the diff (E85.1 / E82.2, workflow PR).
- Modules map endpoints into the group `MapWmsApi()` returns, so an endpoint cannot exist outside a version.
- The console and every customer tool are clients of the same API (`ConsoleUsesApiOnlyTests`, E82.1).
