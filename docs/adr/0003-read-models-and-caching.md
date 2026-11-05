# ADR 0003: Read models, per-instance caching and HTTP validation by row version

**Status:** Accepted (E1.12) · **Date:** 2026-09-19

## Context

Supervisors watch dashboards and lists all shift; those reads must stay fast and must never risk changing
data (E1.12). Repositories serve writes (ADR 0002) and are the wrong shape for projections. Every versioned
table (`IVersionedEntity`, E5.1, [DATABASE-CONVENTIONS](../DATABASE-CONVENTIONS.md)) carries a `row_version`
column, which is a free, monotonic change signal per row and per table.

## Decision

1. **One query class per read.** Console and device reads go through a query class in Infrastructure
   (`OpenReleasesQuery`, `PickerActivityQuery`, …) that projects with LINQ and `AsNoTracking` straight to
   the API record. Hand-written SQL is allowed only inside the same class when a plan requires it; a rollup
   table (maintained by a job) only when the raw tables are measurably too slow. No repository is used for a
   screen or a report.
2. **Caching is per instance, in memory, invalidated by row version.** A `VersionedCache<T>`
   (`Wolfgang.Wms.Core.Caching`) holds one read model and asks `IRowVersionSource` for the `RowVersionStamp`
   of the entity types it was built from (the highest `row_version` plus the row count, so a delete is seen as
   well as an insert or update), at most once per poll interval (a few seconds); the value is rebuilt only
   when that stamp changes. Callers name entity types, never tables: storage names stay in Infrastructure,
   which maps each type through the model. No shared cache component (no Redis, no distributed cache);
   every process holds its own copy and the database is the truth. Database memory-optimized tables are a
   later per-provider tuning option, not part of the design.
3. **Browser and device caching is HTTP validation, never time-based staleness.**
   - A single resource answers with `ETag` = its `row_version` (`EntityTag.FromRowVersion`) and replies
     `304 Not Modified` to a matching `If-None-Match` from the version column alone, without loading the body.
   - A list or report derives its tag from the highest `row_version` in scope plus the row count
     (`EntityTag.FromCollection`, `"<version>|<count>"` in hex), so inserts, updates and deletes all change it.
   - API responses send `Cache-Control: private, no-cache` (`CacheControl.Api`): keep a copy, always
     revalidate, never `no-store` (it kills the 304 path), never `max-age`.
   - Static assets publish under content-hashed names with `Cache-Control: public, max-age=31536000,
     immutable` (`CacheControl.StaticAsset`).
   - Devices use the same validation for task lists, settings and manifests.
   `ConditionalResults.GetOrNotModified` is the one place an endpoint does this.

## Consequences

- A screen's cost is one version query per poll interval plus a 304 per client refresh; the body is produced
  only when something changed.
- Read models never go through the change tracker, so a read cannot write.
- Adding a read means one query class and one endpoint that wraps it in `GetOrNotModified`; there is no cache
  key to invent and no invalidation to forget, because the version column does both.
- A second process instance sees changes within one poll interval; the design accepts that window.
- The per-provider `IRowVersionSource` and the compare in `EntityTag` assume `row_version` is monotonic per
  table; the EF stories (E2–E5) must map it that way on both providers. Only versioned entity types carry the
  column, so a read model may name only those in its `IRowVersionSource` call.

## Amendment (2026-10-12): caches as decorators, own writes drop them

A per-instance cache over a store is a decorator on the store's interface (`CachedSettings`,
`CachedSessionRevocations`), not a field inside the store: the store stays plain, the cache is one class with
one job, and the composition is one line in the container. A cache names the entity types it watches
(`IWatchingCache`). Writes by other instances still arrive through the row-version probe within one poll
interval; writes by this instance drop every cache watching a type that was just saved, whichever store
saved it, through a save interceptor (`OwnWritesInvalidation`) that fires after the save, or after the
commit when the save ran inside a transaction. A store that sets a column another cache depends on (a
password change moving a user's "sessions valid after") therefore needs no knowledge of that cache.
