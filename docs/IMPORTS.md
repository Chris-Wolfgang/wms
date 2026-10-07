# Master data import (E16.6)

One contract for every bulk load: the console upload, the file drop and the ERP all call the same endpoints
and get the same result. A file carries one entity type, every row is an idempotent upsert by its natural
key, a bad-row policy is chosen per run, and the run always produces a result: counts plus one line per row.

The field definitions are generated into the docs from the row types the API deserializes:
see the [import formats reference](../docfx_project/docs/reference/import-formats.md) (`zones`, `locations`;
SKUs, barcodes, paths and validation profiles join as their entities land).

## Files

- **JSON** (canonical): an array of the same objects the API takes (`ZoneDraft`, `LocationDraft` with the zone
  named by `zoneCode`), each with an optional `action`. XML (with a published XSD, via ETL-Xml), CSV (header row,
  the documented columns) and zip bundles applied in dependency order follow the release-file work (E24.2) and
  reuse this contract unchanged.
- **One file per entity type**, loaded in dependency order: zones before locations (a location names its zone
  by code and the zone must already exist).
- **At most 10 000 rows per file**; split larger loads.
- `type` and `action` are enums: an absent property lands on the first value (`Pick`, `Upsert`), which is why
  the reference page lists them as optional; the OpenAPI schema lists them as required, so a generated client
  always sends them.

## Endpoints (`/api/v0`, permission `imports.write`)

| Endpoint | Default policy | Natural key |
|---|---|---|
| `POST /sites/{siteId}/imports/zones` | `all_or_nothing` (structural: a partial load leaves the site inconsistent) | `code` |
| `POST /sites/{siteId}/imports/locations` | `accept_valid_rows` (independent rows: the good ones load) | `code` |

Query parameters: `policy=all_or_nothing|accept_valid_rows|validate_only`, `format=json` (default) or
`format=csv` (the row list as a downloadable CSV with the header `row,key,outcome,code,message`).

Request-level problems answer at once: `404 imports.site_not_found`, `400 imports.invalid` (no rows, too many
rows, an unknown policy or format), `503 imports.unavailable` without a database.

## Rows

- `action: Upsert` (the default) inserts a new key or updates an existing one; re-running a file is safe and
  reports `Unchanged` for rows that already match.
- `action: Delete` retires the row (`isActive: false`); rows are never removed, so history keeps its names.
- Zones: `type: Resolution` fails the row, and a resolution zone that already exists is never overwritten or
  retired by a file; those are managed in the console or the API (E16.2). Retiring a zone (by `Delete` or
  `isActive: false`) is refused while zone groups are open in it.
- Locations: the zone must exist; the barcode must be free (a barcode another bin holds fails the row; relabel
  that bin in its own row first); the walk sequence must start with the zone's walk-order prefix.
- Validation covers the field rules, the referential checks and duplicates within the file (the second row
  with a key or a barcode seen earlier fails with `imports.duplicate_in_file`).

## Policies

| Policy | Writes when | Row outcomes reported |
|---|---|---|
| `all_or_nothing` | no row failed | what happened; when any row failed, what *would* have happened and `written: false` |
| `accept_valid_rows` | at least one row has something to write | what happened to each row; failed rows are reported, not loaded |
| `validate_only` | never | what would happen |

All writes of a run go in one transaction. The new zones a run inserts get their settings scope populated at
once (E16.4).

## Result

```json
{ "entity": "zones", "policy": "AcceptValidRows", "written": true,
  "inserted": 2, "updated": 0, "deleted": 0, "unchanged": 0, "failed": 1,
  "rows": [ { "row": 1, "key": "A01", "outcome": "Inserted", "code": null, "message": null },
            { "row": 3, "key": "RES", "outcome": "Failed", "code": "imports.resolution_zone", "message": "…" } ] }
```

The same shape is shown in the console and downloadable as CSV. Raising an issue with the result attached
when rows fail is the issues work (E50); until then the result is the record.
