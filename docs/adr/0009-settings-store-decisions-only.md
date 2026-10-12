# ADR 0009: Settings store decisions only; effective values are resolved, never materialised

**Status:** Accepted (E6.2, E7) · **Date:** 2026-10-12

## Context

A setting (E6) has one value in force at every scope of the hierarchy organisation → site → zone, or
organisation → site → SKU for product policies (E7.4). The E6.2 storage and the E7 cascade as first written
kept that value *materialised*: `core.setting` held one row per (scope, key) with two columns,
`configured_value` (the administrator's decision, or null when the scope inherits) and `effective_value` (what
applies there after the cascade), a change at a parent rewrote the effective value of every inheriting
descendant in the same transaction, and `ISettings.PopulateAsync` gave a newly created site, zone or SKU a row
for every setting allowed there, so that "no record is ever unresolved".

That shape does not scale with the product's own numbers. Rows are scopes × keys: an installation with 6 sites,
10,000 SKUs and eight SKU-scoped policies carries about 480,000 setting rows, almost all of them copies of a
site's value. A site-level change rewrites every inheriting SKU row in one transaction and writes one audit
header and detail per row (E6.4). A SKU that moves between sites keeps stale copies until something re-runs the
cascade. The in-process snapshot the accessor reads from (ADR 0003) loads the whole table, so every instance
holds and reloads tens of thousands of rows to answer a handful of distinct values. And a scope that missed its
population (a key added by a later release, a zone created by a path that did not call `PopulateAsync`) is
served correctly by the read-time walk but never by a stored row, so the materialised rows were already not the
source of truth; the walk was.

## Decision

- **A row records a decision, nothing else.** `core.setting` holds one row per (scope, key) *only where an
  administrator set a value or a cascade mode there*: `configured_value` (null only while the row delegates) and
  `cascade_mode` (E7.2). The `effective_value` column is gone. A reset removes the decision: the row is
  soft-deleted (`deleted_at`), which is how devices see it go (E5.3); writing at that scope again revives it.
- **The effective value is a function, computed on read.** `Resolve(key, scope)` walks the scope's ancestor
  chain, nearest first, and answers the first configured value it meets, else the key's default (E6.1). A
  scope that delegates (`cascade_mode` other than `value`) holds no value and is skipped like an inheriting
  one; the rule that a scope an ancestor delegates past may not be written (`settings.decided_elsewhere`,
  E7.2) is unchanged, because it is a rule about writes. The walk is the same code for every caller: the API,
  the console, the CLI, and the handheld once it resolves locally.
- **Reads stay cheap.** The accessor keeps the per-instance snapshot of ADR 0003, now holding only the
  decision rows, invalidated by the table's `row_version` watermark. A cold read is one query over the
  ancestor chain, at most three rows, through the existing unique index on (scope type, scope id, key).
- **Writes touch one row.** A change at any scope writes that scope's row and nothing else; descendants see it
  through resolution. The audit trail records the decision once. `PopulateAsync` and the cascade are removed;
  creating a site, zone or SKU creates no setting rows.
- **Sync carries decisions.** A device syncs the decision rows (hundreds, not hundreds of thousands) and
  resolves with the same walk against the hierarchy it already holds from the catalog; a site-level change is
  one row in the delta.

The invariant "no record is ever unresolved" is replaced by "resolution is a pure function of the decision
rows, the hierarchy and the registry defaults", which cannot drift: there is no copy to fall out of step.

## Consequences

- Listing the effective value at every scope (an export, a comparison table in the console) is a computation
  over the decision rows and the scope list rather than a table scan; it is cheap in memory and needed only on
  demand.
- Resolution needs the hierarchy (a SKU's site, a zone's site). `ISettingScopeHierarchy` already supplies it;
  the handheld needs the same parent lookup from its synced catalog.
- `SettingValue` keeps `configuredValue`, `effectiveValue` and `inheritedFrom` on the API; `effectiveValue` is
  now computed per response rather than read from a column, which no client can observe.
- Nothing has shipped yet (no release carries `core.setting`), so the column is dropped by a migration with
  no data to carry over; the migration also removes the inherited copies the earlier cascade stored, which are
  no longer decisions.
- Rejected: keeping materialised rows only for zones (bounded) and resolving SKUs. Two code paths for one
  concept, and the zone rows would still be copies that can drift; the saving is not worth the second model.
