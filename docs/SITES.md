# Sites (E16.1)

A site is one warehouse: the operational level everything below the organization (E16.0) is scoped to.
Pickers work at a site, role assignments can be limited to one (`siteId` route values and the
`X-Wms-Site` header, E10.3), and the settings cascade runs organisation → site → zone / SKU (E7). Zones
(E16.2) sit inside a site.

## Fields

| Field | Required | Notes |
|---|---|---|
| `code` | yes | 1–32 letters, digits, `-` and `_`; unique without regard to case (`dc1` and `DC1` are the same site); kept as entered |
| `name` | yes | 1–128 characters; the name users see |
| `timeZone` | yes | an IANA (`Europe/Berlin`) or Windows time zone id the host knows; what the site's clocks, cut-offs and digests use |
| `isActive` | no (default `true`) | `false` retires the site: it stays in lists, reports and history but takes no new work |

Every row is audited with the signed-in user (E6.4) and versioned (E5.1); there is no delete. A site that
was ever used stays, retired, so its history keeps its name.

## API (`/api/v0`)

| Endpoint | Permission | Answers |
|---|---|---|
| `GET /sites` | `sites.read` (Supervisor, Support, Viewer by default) | every site, active and retired, in code order |
| `GET /sites/{siteId}` | `sites.read` | the site with `ETag`; `404 sites.not_found` |
| `POST /sites` | `sites.write` (administrators) | `201` with `ETag`; `409 sites.code_taken`; `400 sites.invalid` naming the field |
| `PUT /sites/{siteId}` | `sites.write` | `If-Match` required (`428` without it, `412` when stale); `200` with the new `ETag`; `409 sites.code_taken`; `409 sites.has_open_releases` |

A grant scoped to a site (`sites.write@site:3`) applies to that site's own endpoints only, because the
`siteId` route value is the one the permission check reads; creating sites and reading the list need an
organisation-level grant. Without a database every endpoint answers `503 sites.unavailable`.

## Retiring a site

`PUT` with `isActive: false` on an active site asks `IOpenReleases` how many releases are still open there
and refuses with `409 sites.has_open_releases` while the answer is not zero. Release intake (E22) supplies
the stored answer; until it lands the registered implementation reports none, so nothing blocks the change.
Reactivating a site is a plain `PUT` with `isActive: true`.

## The settings cascade

`AddWmsDatabase` replaces the organisation-only scope hierarchy with one over the stored sites: the
organisation's children are every site (retired ones included, so their effective values stay consistent
for their history), and a site's parent is the organisation. Setting a value at the organisation therefore
rewrites the effective value of every site that inherits it (E7.1), and `POST /sites` followed by
populating the new site's scope gives it the organisation's current values.

## Defaults on creation (E16.4)

- **Time zone.** A draft without `timeZone` (null or blank) takes the organisation's default time zone (E16.0).
  Until the organisation exists there is no default and the draft is refused with `400 sites.invalid`
  ("timeZone is required"). The console's create form suggests the browser's time zone and shows the
  organisation's as the inherited value.
- **Settings.** The moment a site is created its settings scope is populated: every setting allowed at site
  level gets a row carrying the organisation's current effective value (E7.3), so nothing beneath the new site
  is ever unresolved. The create screen reads the organisation's values (`GET /settings/organization/0`)
  to show what the site will inherit and lets the user override only what differs, afterwards, at
  `/settings/site/{id}`.
- **Everything beneath a site** (zones, locations, paths, SKUs, barcodes, validation profiles) can be created one
  at a time through the API or loaded in bulk through the master data import contract (E16.6), which serves
  the console upload, the file drop and ERP pushes alike.

## Site scope (E16.3)

Every endpoint under `/sites/{siteId}/…` is gated by the route's site: a grant at the organisation or at
that site admits the caller, anything else is `403`. Collection endpoints without a site in the route
(`GET /sites` today; the cross-site picking lists later) are marked `RequirePermissionInScope`: a caller who
holds the permission at the organisation or at any site is admitted, and the store filters the rows through
`SiteScope` (`SiteScope.Of(user, permission)`: unrestricted for an organisation-level grant, else exactly the
sites of the caller's site-level grants) with the one query helper `InScope` (a translated
`site_id IN (...)`). A supervisor granted at one warehouse therefore lists that warehouse only, and never
sees another's rows; a caller with no grant of the permission anywhere is refused. Retired sites stay in
scope so their history remains readable.
