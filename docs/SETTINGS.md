# Settings

Everything an administrator configures after installation is a setting: defined once in code, stored in the
database, edited in the Configure workspace, and read through one typed accessor. `appsettings.json` holds
only what the process needs before it can reach the database (docs/CONFIGURATION.md, E6.5).

## The registry (E6.1)

A setting is a `SettingKey<T>` (`Wolfgang.Wms.Domain.Keys`, E1.13) declared as a `static readonly` field
in the module that owns it and contributed through `ModuleDescriptor.WithSettings(...)`. The host builds one
`SettingRegistry` from every registered module; the console's settings pages, the generated documentation
and the accessor read it, so a key that is not registered cannot be shown, documented or written. Each key
carries:

| Member | Meaning |
|--------|---------|
| `Name` | Lower-case dotted identifier, `picking.lease_timeout`; unique across all modules (the registry refuses duplicates at startup). |
| `Kind` | How the value is edited and stored: `Boolean`, `Integer`, `Number`, `Enum`, `String`, `Duration`, `Timestamp`, `Secret`, `Json`. Derived from `T` through the codec. |
| `Scopes` | Where it may be configured: `OrganizationToZone` (default), `OrganizationToSku`, `OrganizationToSite`, or `Organization` alone (E7). |
| `DefaultValue` / `DefaultText` | Used when nothing is configured at any scope. |
| `Description` | One line, user-facing. |
| `Validator` | `Func<T, string?>` returning a reason when the kind alone cannot reject a value (a range, a format). |
| `RequiresRestart` | The console warns that the change takes effect after a restart. |
| `TriggersDeviceResync` | Devices resync their settings cache before continuing. |

```csharp
public static class PickingSettings
{
    public static readonly SettingKey<TimeSpan> LeaseTimeout = new("picking.lease_timeout", TimeSpan.FromMinutes(15), "How long a picker holds a task.")
    {
        Validator = v => v >= TimeSpan.FromMinutes(1) && v <= TimeSpan.FromHours(1) ? null : "must be between 1 minute and 1 hour",
        TriggersDeviceResync = true,
    };
}

services.AddWmsModule(ModuleDescriptor.Create("picking").WithSettings(PickingSettings.LeaseTimeout));
```

`GET /api/v0/settings/registry` returns every entry (`name`, `kind`, `scopes`, `default`, `description`,
`choices`, `requiresRestart`, `triggersDeviceResync`); a secret's default is masked. Values arrive with
E6.3.

## Stored text and codecs

Every value is stored as invariant-culture text so the console, the API and the CLI write the same bytes on
every engine (E6.3). `SettingCodec<T>` converts between the value and the text; `SettingCodecs.For<T>()`
supplies the built-in codecs:

| `T` | Kind | Stored as |
|-----|------|-----------|
| `bool` | Boolean | `true` / `false` |
| `int`, `long` | Integer | digits |
| `decimal`, `double` | Number | `1.5` (`.` decimal point; `NaN`/infinity rejected) |
| enum | Enum | the name, parsed case-insensitively; `Choices` lists the names |
| `string` | String | as is |
| `TimeSpan` | Duration | `[d.]hh:mm:ss[.fffffff]` |
| `DateTimeOffset` | Timestamp | ISO 8601 round-trip (`2026-09-20T08:30:00.0000000+02:00`) |
| `SecretText` | Secret | plain in the codec; encrypted by the store (E8.3), masked on screen |
| anything else | Json | `SettingCodecs.Json(serialize, deserialize)` with the module's source-generated `JsonTypeInfo`; a key with no codec fails at construction |

Invalid text is reported, never thrown: `key.Validate(text)` returns a reason (kind first, then the
validator) or null, and the registry's `Check(name, scope, text)` maps failures to the module's error codes:
`settings.unknown_key` (404), `settings.scope_not_allowed` (400), `settings.invalid_value` (400).

## Storage (E6.2)

Values live in `core.setting`, one row per (scope type, scope id, key), on both engines through the normal
migrations (`wms migrate`):

| Column | Meaning |
|--------|---------|
| `scope_type`, `scope_id` | The `SettingScopeRef`: `organization` (id 0), `site`, `zone` or `sku` and the row id. |
| `key` | The registered setting name. |
| `configured_value` | What an administrator set at this scope as stored text; null when the scope inherits. |
| `effective_value` | What applies at this scope after the cascade (E7.1); always present. |
| `row_version` | The E6.2 "version": sequence-backed, reassigned by the trigger on every write; the concurrency token, the `ETag` and the device sync watermark. |
| `updated_by`, `updated_at` | Who wrote the row last (a user id, or the service identity for a cascade) and when (UTC). |
| `deleted_at` | Soft delete, so a reset reaches devices as a delta (E5.3). |

The unique index `ux_setting_scope_type_scope_id_key` serves the accessor's lookup and enforces one row per
scope and key. Nothing writes the table directly: the accessor (E6.3) validates against the registry, writes
the row, recomputes descendants and audits the change.

## Reading and writing (E6.3)

`ISettings` (`Wolfgang.Wms.Core.Settings`) is the one way to read or change a setting; the console, the API
and the CLI all go through it, and nothing writes `core.setting` directly.

- `GetAsync(key, scope)` returns the effective value at a scope: its own configured value, else the nearest
  ancestor's, else the key's default; `T` is inferred from the key. Reads are served from a per-instance
  snapshot of `core.setting` (`SettingsCache`, ADR 0003) reloaded only when the table's highest
  `row_version` moves, probed at most every 5 seconds, and dropped outright by this instance's own writes.
- `SetAsync(key, scope, value, updatedBy)` checks the key is registered, the scope allowed and the value
  accepted by the validator, stores the invariant text as `configured_value` and `effective_value`, walks
  the hierarchy down rewriting the effective value of descendants that inherit (a descendant with its own
  configured value keeps it and shields its subtree, E7.1/E7.2), saves it all in one transaction and
  invalidates the cache. `ResetAsync` clears the configured value so the scope inherits again and cascades
  the inherited value the same way. `SetTextAsync`/`FindAsync` are the same operations by name for the API.
- Failures are `SettingException`s carrying an error code; the module's exception handler answers the
  matching problem: `settings.unknown_key` (404), `settings.unknown_scope` (400), `settings.scope_not_allowed`
  (400), `settings.invalid_value` (400), `settings.store_unavailable` (503, before a database is configured;
  reads then answer defaults).
- `ISettingScopeHierarchy` supplies parents and children. Until sites, zones and SKUs are entities, the
  placeholder knows only that a site's parent is the organisation; E7.1 replaces it.
- Secrets are masked in every `SettingValue` and stored encrypted (`enc:v1:`, E8.3) through `ISecretProtector`;
  only `GetAsync<SecretText>` sees the plain value.

| Endpoint | Meaning |
|----------|---------|
| `GET /settings/registry` | Every registered setting (E6.1). |
| `GET /settings/{scope}/{id}` | Every setting at a scope: `configuredValue`, `effectiveValue`, `inheritedFrom` (`organization`, `site:3`, `default` or null when configured here), `rowVersion`, `etag`, `updatedBy`, `updatedAt`. |
| `GET /settings/{scope}/{id}/{key}` | One setting; the `ETag` header is the stored row's version. |
| `PUT /settings/{scope}/{id}/{key}` | Body `{ "value": "text" }` (null resets). `If-Match` with the row's tag is required once a row exists (428/412, E5.2); the first write at a scope has no row. |
| `DELETE /settings/{scope}/{id}/{key}` | Reset to inherit; same `If-Match` rule. |

`{scope}` is `organization` (id 0), `site`, `zone` or `sku`. Writes record the caller as `updatedBy`
(`anonymous` until E9).

## Audit (E6.4)

Every change to `core.setting` is recorded by the AuditTrail library, not a hand-written table: `WmsDbContext`
derives from `AuditingDbContext` (Model 1, required because the connection retries on transient failures),
so each save writes one `core.audit_header` row per changed row (`user_id` = the host's application name,
`on_behalf_of_user_id` = the signed-in user, entity, key, `I`/`U`/`D`, `transaction_id` shared by everything
one save changed, `audited_at_utc`) and one `core.audit_detail` row per changed column, in the same
transaction as the change. Deleted rows keep their last values. The same store serves every audited table
(master data, roles, leases, API keys, validation profiles); high-volume picking tables opt out with
`[NotAudited]`. The console's audit viewer, grouping headers by transaction, arrives with the console
stories; ordering within a transaction and a WMS correlation on the header are upstream requests
(Chris-Wolfgang/AuditTrail#344, #345; DATABASE-CONVENTIONS.md, "Audit tables").

## Cascade modes and inheritance on creation (E7)

A change at a parent flows down in one transaction (E7.1): every descendant that inherits gets the new
effective value, and a descendant with its own configured value keeps it and shields its subtree.

A scope can also hand the decision down instead of holding a value (E7.2). `PUT` with `{ "mode": "per_site" }`
(or `per_zone`, `per_sku`) sets the scope's `cascadeMode`: it drops its own configured value, inherits for
display, and the scopes it delegates to decide. `allowedModes` on every `SettingValue` lists what the key
permits there: an organisation may delegate to any scope type below it that the key allows, a site to zones
or SKUs, a zone or a SKU only holds a value. While an ancestor delegates past a scope (organisation
`per_zone` and a write at a site), the write is refused with `settings.decided_elsewhere` (409); the deciding
level and everything above it write normally, and a value written at a delegating scope switches it back to
`value`. The console shows overridden children greyed with their retained values (`configuredValue` is
present while `inheritedFrom` is null).

`ISettings.PopulateAsync(scope)` (E7.3) gives a newly created site, zone or SKU a row for every setting
allowed there, carrying the inherited effective value, so no record is ever unresolved; the create paths of
those entities call it when they arrive. SKU-scoped keys (E7.4) declare `SettingScopes.OrganizationToSku`
and cascade organisation → site → SKU through the same code; the hierarchy answers SKU parents once SKUs are
entities.

## Scopes (E6.2, E7)

A value lives at a `SettingScopeRef`: a scope type (`organization`, `site`, `zone`, `sku`) and the id of the
site, zone or SKU (0 for the single organisation). Values cascade organisation → site → zone, or
organisation → site → SKU for product policies; a child without its own configured value takes its parent's
effective value. The cascade follows in E7.
