# Troubleshooting

Every error the API answers with carries a stable `code` and a `type` link into this page (E82.3). Find your
code below: what it means, and what to do. The full list with statuses and messages is the
[error codes reference](reference/error-codes.md); `ReferenceDocsTests` fails the build when a code has no
entry here.

## Sign-in and sessions (`auth.*`)

<a id="auth-invalid-credentials"></a>
### `auth.invalid_credentials`
The user name or password is wrong. The message is the same for an unknown user and a wrong password, so
names cannot be probed. Check the spelling and the keyboard layout; an administrator can reset the password.

<a id="auth-locked-out"></a>
### `auth.locked_out`
Too many failed sign-ins locked the account until the time in the message (`auth.lockout_minutes`). Wait
for the lock to expire, or ask an administrator to reset the password, which clears the lock.

<a id="auth-disabled"></a>
### `auth.disabled`
The account is disabled. Only an administrator can enable it again.

<a id="auth-integrity-failure"></a>
### `auth.integrity_failure`
The account row does not match its integrity signature: it was changed outside the application or the
integrity key changed. Sign-in is refused as a precaution. An administrator repairs it as described in the
integrity guide (re-sign after verifying the row, or restore from backup).

<a id="auth-not-signed-in"></a>
### `auth.not_signed_in`
The request carried no valid session. Sign in; a session also ends when it expires (`auth.session.lifetime`)
or when an administrator revokes it.

<a id="auth-forbidden"></a>
### `auth.forbidden`
The signed-in user lacks the permission the endpoint checks, everywhere or at the request's site. A role
that carries the permission (see the [permissions reference](reference/permissions.md)) must be assigned.

<a id="auth-password-change-required"></a>
### `auth.password_change_required`
The account still has its bootstrap or reset password. Change it through the console (or
`POST /auth/local/password`); nothing else is allowed until then.

<a id="auth-password-rejected"></a>
### `auth.password_rejected`
A password change was refused: the current password is wrong or the new one fails the policy (length,
not the user name, not the current password). The message says which.

<a id="auth-local-login-closed"></a>
### `auth.local_login_closed`
Local sign-in is closed because single sign-on has been verified on this install and no unlock window is
open. Sign in through the identity provider. If the provider is the problem, an administrator opens a timed
window from the host (`wms-admin unlock --minutes N`, audited) or sets `Wms:Auth:ForceLocal=true` in
`appsettings` as the emergency override.

<a id="auth-provider-not-enabled"></a>
### `auth.provider_not_enabled`
The named identity provider is not in `auth.providers.enabled` or is not registered on this host. Enable it
in the settings, or use a provider the sign-in page offers.

<a id="auth-provider-failed"></a>
### `auth.provider_failed`
The identity provider refused or failed the sign-in; the message names the reason without any secret. Check
the provider's configuration (`auth.oidc.*`), its reachability from the API host, and the provider's own logs.

<a id="auth-mapping-rejected"></a>
### `auth.mapping_rejected`
A group-to-role mapping was refused: the group is blank or the same mapping already exists.

<a id="auth-mapping-not-found"></a>
### `auth.mapping_not_found`
No group mapping has that id; it was removed, or the id is from another environment.

<a id="auth-unavailable"></a>
### `auth.unavailable`
Sign-in is unavailable until a database is configured. Complete the bootstrap (`Wms:Database:*`) and restart.

## Roles and assignments (`auth.roles.*`)

<a id="auth-role-not-found"></a>
### `auth.role_not_found`
No role has that id.

<a id="auth-role-name-taken"></a>
### `auth.role_name_taken`
A role with that name exists; names are unique. Pick another name or edit the existing role.

<a id="auth-built-in-role-read-only"></a>
### `auth.built_in_role_read_only`
Built-in roles cannot be edited or deleted. Copy the role (`POST /auth/roles/{id}/copy`) and edit the copy.

<a id="auth-unknown-permission"></a>
### `auth.unknown_permission`
The role names a permission that is not in the catalog. Only the permissions in the
[permissions reference](reference/permissions.md) can be granted.

<a id="auth-invalid-role"></a>
### `auth.invalid_role`
The role draft is incomplete: the message names the missing or invalid field (name, description).

<a id="auth-assignment-not-found"></a>
### `auth.assignment_not_found`
No assignment has that id; it was already removed.

<a id="auth-user-not-found"></a>
### `auth.user_not_found`
No user has that id.

## Concurrency (`concurrency.*`)

<a id="concurrency-precondition-failed"></a>
### `concurrency.precondition_failed`
The `If-Match` tag sent is not the record's current version: someone else changed it since it was read.
Reload the record, reapply the change and send the new ETag.

<a id="concurrency-precondition-required"></a>
### `concurrency.precondition_required`
An update or delete arrived without `If-Match`. Read the record first and send its ETag, so a stale copy
can never overwrite a newer one.

## Devices (`device.*`)

<a id="device-version-missing"></a>
### `device.version_missing`
The request carried no `X-Wms-Device-Version` header. The handheld app sends it on every call; a tool
calling device endpoints must send its version too.

<a id="device-version-invalid"></a>
### `device.version_invalid`
The header value is not a version (`major.minor.patch`).

<a id="device-version-too-old"></a>
### `device.version_too_old`
The app is older than the site's minimum (`device.min_app_version`). Update the app; nothing else works
until then.

## Licensing (`license.*`)

<a id="license-key-rejected"></a>
### `license.key_rejected`
The pasted key did not verify or is not a key of this release's schema; the message says why (not a signed
document, signature does not verify, unknown tier, newer schema, missing field). Paste the document exactly
as issued; a key for a newer schema needs an upgrade first. See the [licensing guide](licensing.md).

<a id="license-key-not-found"></a>
### `license.key_not_found`
No installed key has that id. The compiled-in free key cannot be removed.

<a id="license-limit-reached"></a>
### `license.limit_reached`
A creation is blocked by a license limit: beyond the allowance, after the grace period, or while coverage
has lapsed. The message names the limit and the tier. Install a larger key or an add-on, or remove
something that counts; nothing already created stops working.

<a id="license-feature-not-licensed"></a>
### `license.feature_not_licensed`
The feature is not in the installed tier. The [feature comparison](licensing/feature-comparison.md) shows
which tier includes it.

## Logging (`logging.*`)

<a id="logging-elevation-rejected"></a>
### `logging.elevation_rejected`
A timed level elevation was refused: the level must lower the threshold (Trace, Debug or Information) and
the duration must be between 1 minute and `logging.elevation_max_minutes`.

## Organization (`organization.*`)

<a id="organization-not-created"></a>
### `organization.not_created`
The install has no organization yet: the first-run wizard's first step has not run. An administrator creates
it with `POST /organization` (or the wizard); until then the login page shows the product name only.

<a id="organization-already-exists"></a>
### `organization.already_exists`
There is exactly one organization per install and it already exists. Edit it with `PUT /organization`
(with `If-Match`) instead of creating another.

<a id="organization-invalid"></a>
### `organization.invalid`
A field is missing, too long, or not what it must be: the message names the field. `timeZone` must be a time
zone id the host knows (`Europe/Berlin`), `locale` a culture name (`en-US`), `logoDataUrl` a `data:image/…`
URL of at most 256 KB, and a contact's `email` an e-mail address.

<a id="organization-unavailable"></a>
### `organization.unavailable`
The organization lives in the database and `Wms:Database:Provider` is `None` (or not set). Configure the
database and restart the API.

## Sites (`sites.*`)

<a id="sites-not-found"></a>
### `sites.not_found`
No site has that id. `GET /sites` lists every site, active and retired, with its id; a site is never deleted,
so an id that once existed still does.

<a id="sites-code-taken"></a>
### `sites.code_taken`
Another site already has that code. Codes are compared without regard to case (`dc1` and `DC1` are the same
site), so pick a different one or edit the existing site.

<a id="sites-invalid"></a>
### `sites.invalid`
A field is missing, too long, or not what it must be: the message names the field. `code` is 1-32 letters,
digits, `-` and `_`; `name` is at most 128 characters; `timeZone` must be a time zone id the host knows
(`Europe/Berlin`, `America/Chicago`).

<a id="sites-has-open-releases"></a>
### `sites.has_open_releases`
The site cannot be retired (`isActive: false`) while releases are open against it: the message says how
many. Complete or cancel them first, then retry; or leave the site active and stop assigning work to it.

<a id="sites-unavailable"></a>
### `sites.unavailable`
Sites live in the database and `Wms:Database:Provider` is `None` (or not set). Configure the database and
restart the API.

## Zones (`zones.*`)

<a id="zones-site-not-found"></a>
### `zones.site_not_found`
No site has the `siteId` in the route. `GET /sites` lists every site with its id.

<a id="zones-not-found"></a>
### `zones.not_found`
No zone of that site has that id. `GET /sites/{siteId}/zones` lists the site's zones, active and retired; a zone
is never deleted, and a zone of another site answers this too.

<a id="zones-code-taken"></a>
### `zones.code_taken`
Another zone of the same site already has that code. Codes are compared without regard to case within a site
(`a01` and `A01` are the same zone); the same code in another site is fine.

<a id="zones-invalid"></a>
### `zones.invalid`
A field is missing, too long, or not what it must be: the message names the field. `code` is 1-32 letters,
digits, `-` and `_`; `name` at most 128 characters; `walkOrderPrefix` at most 16; `type` is `Pick`, `Bulk` or
`Resolution`; `isRejectLane` is for pick zones only; `resolution` is required for a resolution zone and absent
otherwise, and its `resolverUserIds` must be existing users, each named once.

<a id="zones-has-open-groups"></a>
### `zones.has_open_groups`
The zone cannot be retired (`isActive: false`) while zone groups are open in it: the message says how many.
Let the pickers complete them first, then retry.

<a id="zones-unavailable"></a>
### `zones.unavailable`
Zones live in the database and `Wms:Database:Provider` is `None` (or not set). Configure the database and
restart the API.

## Locations (`locations.*`)

<a id="locations-site-not-found"></a>
### `locations.site_not_found`
No site has the `siteId` in the route. `GET /sites` lists every site with its id.

<a id="locations-not-found"></a>
### `locations.not_found`
No location of that site has that id. `GET /sites/{siteId}/locations` pages through the site's bins; a bin is
never deleted, and a bin of another site answers this too.

<a id="locations-zone-not-found"></a>
### `locations.zone_not_found`
`zoneId` does not name a zone of this site. `GET /sites/{siteId}/zones` lists the site's zones with their ids;
a zone of another site cannot hold this site's bins.

<a id="locations-code-taken"></a>
### `locations.code_taken`
Another bin of the same site already has that code. Codes are compared without regard to case within a site;
the same code in another site is fine.

<a id="locations-barcode-taken"></a>
### `locations.barcode_taken`
Another bin of the same site already carries that barcode. A scan must resolve to one bin, so relabel one of
them.

<a id="locations-invalid"></a>
### `locations.invalid`
Either the draft or the page request is not what it must be: the message names the problem. Drafts: `code`
is 1-64 letters, digits, `-` and `_`; `barcode` and `walkSequence` are 1-128 and 1-64 visible ASCII characters
without spaces or `|`; `walkSequence` must start with the zone's walk-order prefix when the zone has one.
Page requests: `sort` is one of `walk_sequence`, `code`, `barcode`, `id` (with a leading `-` for descending),
`after` and `before` are cursors this API issued under the same sort and never both at once, and `id_from`
must not exceed `id_to`. After changing the sort, start again from the first page.

<a id="locations-unavailable"></a>
### `locations.unavailable`
Locations live in the database and `Wms:Database:Provider` is `None` (or not set). Configure the database
and restart the API.

## Imports (`imports.*`)

<a id="imports-site-not-found"></a>
### `imports.site_not_found`
No site has the `siteId` in the route. `GET /sites` lists every site with its id.

<a id="imports-invalid"></a>
### `imports.invalid`
The request, not a row, is wrong: the file has no rows or more than 10 000 (split it), `policy` is not one of
`all_or_nothing`, `accept_valid_rows`, `validate_only`, or `format` is not `json` or `csv`. A row-level
`imports.invalid` means the row has no key (`code`).

<a id="imports-duplicate-in-file"></a>
### `imports.duplicate_in_file`
Row level: the row's key (or, for locations, its barcode) appears in an earlier row of the same file. Keys are
compared without regard to case. Keep one row per key; the first wins and the later ones fail.

<a id="imports-reference-not-found"></a>
### `imports.reference_not_found`
Row level: the row names something the site does not have, such as a `zoneCode` for a location. Load the
zones file first, or fix the code.

<a id="imports-key-not-found"></a>
### `imports.key_not_found`
Row level: `action: Delete` names a key that does not exist in the site. Nothing to retire; remove the row or
fix the key.

<a id="imports-resolution-zone"></a>
### `imports.resolution_zone`
Row level: resolution zones are created and edited in the console or the API only (E16.2). A row with
`type: Resolution`, or a row whose code is an existing resolution zone, is refused.

<a id="imports-unavailable"></a>
### `imports.unavailable`
Imports write to the database and `Wms:Database:Provider` is `None` (or not set). Configure the database and
restart the API.

## Copies (`copies.*`)

<a id="copies-not-found"></a>
### `copies.not_found`
The source (site, zone, location), the target site or a zone named in the request does not exist, or no
location's code starts with the range's `codePrefixFrom`. The message says which. `GET /sites`,
`GET /sites/{siteId}/zones` and `GET /sites/{siteId}/locations` list the ids and codes.

<a id="copies-invalid"></a>
### `copies.invalid`
The request is not what it must be: `codePrefixFrom`/`codePrefixTo` (or `walkPrefixFrom`/`walkPrefixTo`) given
only half, the new code or name failing the entity's rules, a copied location's walk sequence outside its
zone's walk-order prefix, or a zone copied with its locations within the same site without a substitution.

<a id="copies-code-taken"></a>
### `copies.code_taken`
The copy would need a code already in use: a site code, a zone code within the target site, a bin code within
the site, or the substitution gives two copies the same code. Choose another code or prefix.

<a id="copies-barcode-taken"></a>
### `copies.barcode_taken`
The copy would need a barcode already in use within the site, or the substitution gives two copies the same
barcode. A barcode that does not start with `codePrefixFrom` is kept as it is, so within one site it clashes
with its source: relabel it in the copy, or copy into another site.

<a id="copies-unavailable"></a>
### `copies.unavailable`
Copies write to the database and `Wms:Database:Provider` is `None` (or not set). Configure the database and
restart the API.

## Settings (`settings.*`)

<a id="settings-unknown-key"></a>
### `settings.unknown_key`
No module declares a setting of that name. The [settings reference](reference/settings.md) lists every one;
names are case-sensitive.

<a id="settings-scope-not-allowed"></a>
### `settings.scope_not_allowed`
The setting cannot be configured at that scope (its declaration lists the allowed scopes). Configure it at
an allowed scope; descendants inherit.

<a id="settings-unknown-scope"></a>
### `settings.unknown_scope`
The scope type in the URL is not one of `organization`, `site`, `zone`, `sku`.

<a id="settings-store-unavailable"></a>
### `settings.store_unavailable`
A write arrived before a database was configured. Settings are read-only defaults until the bootstrap
completes.

<a id="settings-mode-not-allowed"></a>
### `settings.mode_not_allowed`
The cascade mode requested is not allowed for that setting at that scope (for example delegating below the
lowest allowed scope).

<a id="settings-decided-elsewhere"></a>
### `settings.decided_elsewhere`
An ancestor delegated the decision past this scope, so the value is decided lower down (the message says
where and by whom). Configure it there, or change the ancestor's cascade mode back to `value`.

<a id="settings-invalid-value"></a>
### `settings.invalid_value`
The value does not parse as the setting's kind or the validator rejected it; the message says what is
expected.
