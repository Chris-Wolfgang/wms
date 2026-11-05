# Licensing (E79)

The product runs with no key on the compiled-in **free tier**. A paid key is pasted into the console, verified
offline against the vendor's public key, stored encrypted as a setting and in force on every instance within
seconds. What a tier grants is explicit and only ever grows: a release can add a feature to a tier or raise a
limit, never the reverse.

## The model (E79.1)

| Term | Meaning |
|---|---|
| **Feature** | A capability a tier grants, named individually (`LicenseFeatures`, e.g. `workspace.insights`). A feature the tier table and the keys do not name is off; "empty means everything" never occurs. |
| **Limit** | A ceiling (`LicenseLimits`: `sites`, `devices`, `users`, `max_totes_per_picker`). Every tier values every limit; `unlimited` is a value. |
| **Tier table** | This release's map tier → features and limits (`TierTables.Current`), compiled in. Three tiers: free, pro, enterprise. |
| **Base key** | One per install: the tier, the organization it binds to, the paid coverage periods. The free tier is a base key with no coverage and no organization. |
| **Add-on key** | Adds devices, a feature or a raised limit to whatever base is in force; any number stack. |
| **Effective license** | Base tier resolved through the table + explicit entries in the base key + every valid add-on. `ILicense.Current` on every host. |

Keys carry their own `schema_version` (independent of the product and API versions); a key written for a
newer schema is refused with "upgrade first".

### Monotonic tables

Two checks run in the unit tests, so CI fails a pull request that breaks them:

- **Complete**: every tier defines every limit; every name in the table is a catalogued feature or limit; a
  higher tier never grants less than a lower one (`TierTable.Verify`).
- **Additions only**: `docs/licensing/tier-table.txt` (in the repository) holds the last released table. The current table may
  add tiers, features and raise limits over it, never remove or lower (`TierTable.Regressions`). The release
  cut refreshes the file from `TierTables.Current.ToLines()`.

New capability that belongs in a higher tier ships as a **new feature name** (`reports.custom_views` in Pro
while `reports.builtin` stays free); a feature never moves up a tier.

## The free tier (E79.2)

Compiled in, never stored, changes only with a release. Limits: 1 site, 5 connected devices, unlimited users
and pickers, 1 tote per picker. Features: every v1 capability except the paid ones — `workspace.insights`,
`reports.custom_views`, `picking.bulk`, `messaging.picker_to_picker`, `devices.remote_logging`,
`devices.bulk_enrollment`. The full table per release is the [feature comparison](licensing/feature-comparison.md).

## Keys (E79.3, E79.11)

A key is a signed JSON document: `{"payload":"<base64url JSON>","signature":"<base64url ES256>","algorithm":"ES256"}`.
The signature covers the encoded payload bytes (no canonicalisation), made with the vendor's P-256 private
key; the product embeds only the public half (`LicenseVerifier.VendorPublicKey`). Payload fields
(snake_case): `schema_version`, `key_id`, `kind` (`base`/`add_on`), `tier`, `organization`, `coverage`
(`[{from,to}]`), `features`, `limits` (`{name: n | null}`), `devices`, `supersedes`, `issued_at`,
`allowance_percent`, `allowance_minimum_units`, `grace_days`.

Install: `PUT /api/v0/system/license/keys` with `{"key": "<document>"}` (permission `license.manage`). The
document is verified, stored as one line of the secret setting `license.keys` (encrypted at rest, masked on
the settings page), and the license is recomputed at once on that instance and within five seconds on the
others (`LicenseSync`). A key with the same `key_id` replaces the old one. `DELETE
/api/v0/system/license/keys/{keyId}` removes one. Nothing needs a restart.

Composition rules:

- The newest issued base key (by `issued_at`) is the base; other base keys are listed as *extra* and ignored.
- An add-on counts when it is bound to the base's organization (the free base accepts any), its own coverage
  (if any) covers this release, and the base is in coverage. A lapsed base freezes the whole stack.
- A key named in another key's `supersedes` is ignored and listed as *superseded*.
- Explicit entries win: a feature listed in the base is granted; a limit listed in the base replaces the
  tier's; an add-on's limit only ever raises.
- Devices: the tier includes 5; add-ons add theirs ("5 included + 10 purchased = 15").
- A document that does not verify stays in the list as *invalid* with the reason, so the page shows it.

## Enforcement (E79.4, E79.8)

One gate: `LicenseGate.CheckAsync(limit, adding)` (or `RequireAsync`, which throws the
`license.limit_reached` problem, HTTP 403). The count is **recomputed from the data at every check**
(`ILicenseUsage`), never cached or incremented, so a row inserted outside the application cannot slip past.
The sites and devices stories supply the counters; until then every count is zero.

Limits are soft. With ceiling `C`, allowance `A = max(allowance_percent × C, allowance_minimum_units)` and
grace `G` days (from the key; defaults 10 %, 2 units, 30 days; free tier 10 %, 1 unit, 14 days):

| Count after creation | Outcome |
|---|---|
| ≤ C | allowed |
| ≤ C + A, within G days of first going over | allowed with a banner ("7 of 5 devices — 26 days to add licenses") |
| > C + A, or after G days | blocked with a message naming the limit and the tier |
| any, while coverage has lapsed | blocked when over the ceiling; nothing else changes |

The day a limit first went over is the setting `license.overage_since.<limit>`, recorded and cleared by
`LicenseGate.ReconcileAsync` (the license page reconciles every limit on each read; creators call it after a
creation). Dropping back under the limit or installing a larger key clears the state at once. Nothing already
created ever stops working.

Feature edges: an endpoint declares `.RequireLicenseFeature(LicenseFeatures.X)`; a tier without the feature
gets `403 license.feature_not_licensed` naming the feature and the tier.

`max_totes_per_picker` (E79.9): the setting `picking.max_totes_per_picker` cascades organization → site → zone
(the picker override arrives with pickers); the effective value is `min(setting, license)`
(`LicenseSettings.EffectiveMaxTotesPerPicker`). Free tier: 1; pro: 5; enterprise: unlimited.

## Expiry (E79.5)

The free tier never expires. A paid base key carries `coverage`, the paid maintenance periods. **A release is
covered when its release date (`ReleaseInfo.ReleaseDate`) falls inside a covered period.** Releases published
during a lapse are never unlocked by a later term: a customer returning after a gap either buys the gap
(reinstatement) or continues from the last covered version, with the new term covering releases from then on.

After expiry nothing is removed: every feature and limit in effect stays in effect. Blocked while lapsed:
creating sites, enrolling devices beyond the current count (replacing a revoked device is not counted as
more), raising any limit, and any upgrade — the installer and `wms upgrade` refuse a release whose date is
outside the coverage periods, **security patches included**; covered releases stay installable forever. The
console banner shows the expiry and what it blocks. Renewal is a new key with extended coverage; there is no
other penalty.

## The license page (E79.6, E79.10)

`GET /api/v0/system/license` (permission `license.read`): tier, organization, coverage and last covered day,
this release and its date, every feature granted, every limit with its ceiling, count, percentage, warning
flag (`license.usage_warning_percent`, default 80) and standing, the device totals, every installed key with
what it adds and its status, and the banners. `GET /api/v0/system/license/comparison` returns the feature-by-
tier table of this release with the installed tier marked; the same table renders the [feature
comparison](licensing/feature-comparison.md) (regenerate with `WMS_UPDATE_LICENSING_DOCS=1 dotnet test tests/Wolfgang.Wms.UnitTests
--filter FeatureComparisonTests`; the test fails when the page is stale). The page's "what changed" lists the
additions over the last released table.

## Issuing keys (vendor)

`LicenseKeySigner.Sign(key, privateKey)` produces the document; the vendor's private key is held outside the
repository and the product. Tests sign with their own pair and register a `LicenseVerifier` for its public
key before `AddWmsLicenseModule()`.

## Third-party licenses (E79.7)

`license-audit.yaml` gates every `src/` dependency against `.github/license-audit/allowed-licenses.json`;
`scripts/Generate-ThirdPartyNotices.ps1` writes `THIRD-PARTY-NOTICES.md`, which the release attaches.
