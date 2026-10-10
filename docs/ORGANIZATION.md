# Organization (E16.0)

The one row per install that names the company the system belongs to. It is single-tenant by design: there is
exactly one, created by the first-run wizard's first step, edited afterwards, never deleted. Pickers never see
or choose it; sites (E16.1) remain the operational level.

## What it is for

- **The top of the settings cascade.** The scope the settings API calls `organization` (formerly "global") is
  this row: organisation → site → zone / SKU. Its default time zone and locale are what a new site and a new
  user inherit (E16.4).
- **The name users see.** Console header and login page (`GET /organization/public` carries the name and the
  logo before anyone signs in), notifications and digests, printed cards, support bundles, and the install's
  name in multi-install monitoring (E110).
- **The licensee.** A paid license key names the organisation it binds to (`LicenseStatus.Organization`, E79);
  the console shows both so a key pasted into the wrong install is obvious.

## Fields

| Field | Required | Notes |
|---|---|---|
| `name` | yes | 1–128 characters; the name users see |
| `legalName` | no | up to 256 characters; the registered company name |
| `logoDataUrl` | no | a `data:image/…` URL of at most 256 KB (a small PNG or SVG) |
| `timeZone` | yes | an IANA (`Europe/Berlin`) or Windows time zone id the host knows |
| `locale` | yes | a culture name (`en-US`) |
| `address` | no | `line1`, `line2`, `city`, `region`, `postalCode`, `country` |
| `primaryContact` | no | `name`, `email`, `phone`: who the vendor and the install talk to |
| `supportContact` | no | `name`, `email`, `phone`: who users are pointed at; the primary contact when absent |

## API (`/api/v0`)

| Endpoint | Permission | Answers |
|---|---|---|
| `GET /organization/public` | anonymous | `{ name, logoDataUrl }`; `404 organization.not_created` until the wizard ran |
| `GET /organization` | `organization.read` (Supervisor, Support, Viewer by default) | the row with `ETag`; `404 organization.not_created` |
| `POST /organization` | `organization.write` (administrators) | `201` with `ETag`; `409 organization.already_exists`; `400 organization.invalid` naming the field |
| `PUT /organization` | `organization.write` | `If-Match` required (`428` without it, `412` when stale); `200` with the new `ETag` |

Without a database every endpoint answers `503 organization.unavailable`. Every write is audited with the
signed-in user (E6.4) and bumps the row version (E5.1).
