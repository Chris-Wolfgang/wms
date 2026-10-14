# Copying sites, zones and locations (E16.5)

Set up a similar site, zone or bin without re-entering everything. Every copy is an API operation, so the
console and CSV/API clients use the same thing; every copy is audited as a create whose row carries
`copiedFromId`, the id of its source. Transactional data and pickers' assignments are never copied.

## Endpoints (`/api/v0`)

| Endpoint | Permission | Body | Answers |
|---|---|---|---|
| `POST /sites/{siteId}/copy` | `sites.write` | `code`, `name`, `timeZone` (null keeps the source's), `settings`, `zones`, `locations` (each default `true`), `codePrefixFrom`/`codePrefixTo` | `201` the new site |
| `POST /sites/{siteId}/zones/{zoneId}/copy` | `zones.write` | `code`, `name`, `targetSiteId` (null: the same site), `settings`, `locations`, `codePrefixFrom`/`codePrefixTo` | `201` the new zone |
| `POST /sites/{siteId}/locations/{locationId}/copy` | `locations.write` | `code`, `barcode`, `walkSequence` (null keeps the source's), `zoneId` (null: the source's zone) | `201` the new location |
| `POST /sites/{siteId}/locations/copy-range` | `locations.write` | `codePrefixFrom`, `codePrefixTo`, `zoneId` (null keeps each source's), `walkPrefixFrom`/`walkPrefixTo` | `201` the new locations, in source order |

Problems: `404 copies.not_found` (the source, the target site or a zone does not exist, or no location matches
the range), `400 copies.invalid` (a half substitution, an invalid new code or name, a same-site zone copy with
locations but no substitution), `409 copies.code_taken` / `copies.barcode_taken` (a copy would need a code or
barcode already in use, or the substitution gives two copies the same one), `503 copies.unavailable` without
a database.

## What a copy brings

- **Site**: the site's settings overrides (configured values and cascade modes at site scope; secrets are never
  copied), the zones with their reject-lane flags, resolution properties and resolvers and their own settings
  overrides, and the locations. Codes and barcodes are kept unless a substitution is given; a different site
  has its own code space, so no renumbering is needed. The copy's settings scopes are populated first (E16.4),
  then the overrides re-applied. The result opens in the wizard for the fields that must differ.
- **Zone**: the zone with its properties and settings overrides, and its locations. Within the same site the
  locations need a substitution (`codePrefixFrom`/`codePrefixTo`), applied to each code and barcode that starts
  with the prefix, so the copies get codes and barcodes of their own; a barcode that does not start with the
  prefix would clash and the copy is refused (`409`). Across sites the codes may stay.
- **Location**: the same properties with the new code and barcode (and walk sequence or zone when given).
- **Range of locations** (aisle `A` to aisle `B`): every location whose code starts with `codePrefixFrom`, with
  the prefix replaced in the code and the barcode, and `walkPrefixFrom`/`walkPrefixTo` applied to the walk
  sequence when given. All or nothing: one clash refuses the whole range.

All writes of a copy go in one transaction. The copied rows' walk sequences must still start with their
zone's walk-order prefix (E16.2), so renumbering into a zone with another prefix needs `walkPrefixFrom`/
`walkPrefixTo` as well.
