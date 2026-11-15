# Zones (E16.2)

A zone is an area within a site (E16.1): a picking area on the conveyor route, bulk storage that feeds it, or
a resolution zone where exceptions are worked. Zones are the level the settings cascade sits at below the
site (E7), what tote entry scans resolve to (E26), and what locations (E17.1) belong to.

## Fields

| Field | Required | Notes |
|---|---|---|
| `code` | yes | 1–32 letters, digits, `-` and `_`; unique within the site without regard to case; kept as entered |
| `name` | yes | 1–128 characters |
| `type` | yes | `Pick`, `Bulk` or `Resolution` |
| `walkOrderPrefix` | no | up to 16 characters; the sortable prefix the zone's locations' walk sequences start with (E17.1) |
| `isRejectLane` | no (default `false`) | pick zones only: the zone is also the conveyor's error/overflow lane; drives the reject diagnostics (E26.3) |
| `resolution` | for resolution zones | the properties below; absent for pick and bulk zones |
| `isActive` | no (default `true`) | `false` retires the zone; refused while zone groups are open in it |

### Resolution zones

Zero or more per site. They are created only here (console or API), never by ERP or CSV load (the master data
import contract, E16.6, refuses `type: Resolution`); ERP routes may reference one by code once it exists.

| `resolution.` field | Notes |
|---|---|
| `restockingBin` | the bin (a location barcode or label) resolved stock is restocked to; up to 64 characters; null for none |
| `returnsContainer` | the container returns are collected in; up to 64 characters; null for none |
| `resolverUserIds` | the users assigned to resolve here (existing users, each once, at most 64); an empty list means anyone with the permission |
| `acceptsWeightFailures` | totes that failed the weight check are routed here |
| `acceptsShorts` | short picks are resolved here |
| `acceptsAdjustments` | quantity adjustments are resolved here |
| `acceptsMisdirects` | misdirected totes are resolved here |
| `isVirtualQueue` | `true` for a virtual queue of work items, `false` for a physical lane |

Every row is audited with the signed-in user (E6.4) and versioned (E5.1); there is no delete.

## API (`/api/v0`)

| Endpoint | Permission | Answers |
|---|---|---|
| `GET /sites/{siteId}/zones` | `zones.read` (Supervisor, Support, Viewer by default) | every zone of the site, active and retired, in code order; `404 zones.site_not_found` |
| `GET /sites/{siteId}/zones/{zoneId}` | `zones.read` | the zone with `ETag`; `404 zones.not_found` (also for a zone of another site) |
| `POST /sites/{siteId}/zones` | `zones.write` (administrators) | `201` with `ETag`; `409 zones.code_taken`; `400 zones.invalid` naming the field |
| `PUT /sites/{siteId}/zones/{zoneId}` | `zones.write` | `If-Match` required (`428` without it, `412` when stale); `200` with the new `ETag`; `409 zones.code_taken`; `409 zones.has_open_groups` |

The `siteId` route value is the one the permission check reads, so a grant scoped to a site
(`zones.write@site:3`) applies to that site's zones only. Without a database every endpoint answers
`503 zones.unavailable`.

## Retiring a zone

`PUT` with `isActive: false` on an active zone asks `IOpenZoneGroups` how many zone groups are still open in it
and refuses with `409 zones.has_open_groups` while the answer is not zero. Tote entry (E26) and zone group
completion (E27) supply the stored answer; until they land the registered implementation reports none, so
nothing blocks the change.

## The settings cascade

With a database the scope hierarchy runs organisation → site → zone: a site's children are its zones (retired
ones included, so their effective values stay consistent for their history) and a zone's parent is its site.
A value set at the organisation or the site rewrites the effective value of every zone that inherits it
(E7.1); a zone that does not exist has no parent.
