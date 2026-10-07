# Locations (E17.1)

A location is one bin: a place in a zone (E16.2) of a site (E16.1) with a label a picker scans and a position
in the walk order. Locations are what tasks point at (E17.3), what the walk sequence sorts (E17), and what the
master data import loads in thousands (E16.6, E17.2).

## Fields

| Field | Required | Notes |
|---|---|---|
| `code` | yes | the bin code, 1–64 letters, digits, `-` and `_`; unique within the site without regard to case; the site's code format (E17.11) generates it from aisle/bay/level/position |
| `barcode` | yes | the label on the bin, 1–128 visible ASCII characters (no spaces, no `\|`); unique within the site |
| `zoneId` | yes | a zone of the same site |
| `walkSequence` | yes | a sortable string, 1–64 visible ASCII characters; the order a picker walks the bins in; must start with the zone's `walkOrderPrefix` when the zone has one |
| `isPickable` | no (default `true`) | `false` for a bin that holds stock but is never picked from (staging, overflow) |
| `isActive` | no (default `true`) | `false` retires the bin: it stays in history but takes no new stock or tasks |

Every row is audited with the signed-in user (E6.4) and versioned (E5.1); there is no delete.

## API (`/api/v0`)

| Endpoint | Permission | Answers |
|---|---|---|
| `GET /sites/{siteId}/locations` | `locations.read` (Supervisor, Support, Viewer by default) | one page (below); `404 locations.site_not_found` |
| `GET /sites/{siteId}/locations/{locationId}` | `locations.read` | the bin with `ETag`; `404 locations.not_found` (also for a bin of another site) |
| `POST /sites/{siteId}/locations` | `locations.write` (administrators) | `201` with `ETag`; `400 locations.zone_not_found`; `400 locations.invalid` naming the field; `409 locations.code_taken` / `locations.barcode_taken` |
| `PUT /sites/{siteId}/locations/{locationId}` | `locations.write` | `If-Match` required (`428` without it, `412` when stale); `200` with the new `ETag`; the same `400`/`409` answers |

The `siteId` route value is the one the permission check reads, so a grant scoped to a site applies to that
site's bins only. Without a database every endpoint answers `503 locations.unavailable`.

## Paging

The list is the first endpoint on the keyset contract of [API-CONVENTIONS](API-CONVENTIONS.md): it takes
`after` or `before` (an opaque cursor, never both), `sort` (`walk_sequence`, the default, `code`, `barcode` or
`id`, with a leading `-` for descending; the id is always the tiebreaker), `size` (default 50, at most 500),
`id_from`/`id_to` (so parallel clients can split the table) and `zone_id` (only that zone's bins). It answers
`Page<LocationInfo>`: `items` in the requested sort, `nextCursor`/`previousCursor` (null at the ends),
the exact `totalCount` of the filtered scope and its `minId`/`maxId`. A cursor records the sort it was issued
under and is refused (`400 locations.invalid`) under any other: after re-sorting, start from the first page.
Every sort reads one index that ends with the id (`ux_location_site_id_code_normalized`,
`ux_location_site_id_barcode`, `ix_location_site_id_walk_sequence_id`, `ix_location_zone_id_walk_sequence_id`).
