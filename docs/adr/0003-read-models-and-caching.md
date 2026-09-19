# ADR 0003: Read models, per-instance caching and HTTP validation by row version

**Status:** Accepted (E1.12) · **Date:** 2026-09-19

## Context

Supervisors watch dashboards and lists all shift; those reads must stay fast and must never risk changing
data (E1.12). Repositories serve writes (ADR 0002) and are the wrong shape for projections. Every product
table already carries a `row_version` column (ADR 0002 concurrency), which is a free, monotonic change
signal per row and per table.

## Decision

1. **One query class per read.** Console and device reads go through a query class in Infrastructure
   (`OpenReleasesQuery`, `PickerActivityQuery`, …) that projects with LINQ and `AsNoTracking` straight to
   the API record. Hand-written SQL is allowed only inside the same class when a plan requires it; a rollup
   table (maintained by a job) only when the raw tables are measurably too slow. No repository is used for a
   screen or a report.
2. **Caching is per instance, in memory, invalidated by row version.** A `VersionedCache<T>`
   (`Wolfgang.Wms.Core.Caching`) holds one read model and asks `IRowVersionSource` for the highest
   `row_version` across the tables it was built from, at most once per poll interval (a few seconds); the
   value is rebuilt only when that number moves. No shared cache component (no Redis, no distributed cache);
   every process holds its own copy and the database is the truth. Database memory-optimized tables are a
   later per-provider tuning option, not part of the design.
3. **Browser and device caching is HTTP validation, never time-based staleness.**
   - A single resource answers with `ETag` = its `row_version` (`EntityTag.FromRowVersion`) and replies
     `304 Not Modified` to a matching `If-None-Match` from the version column alone, without loading the body.
   - A list or report derives its tag from the highest `row_version` in scope plus the row count
     (`EntityTag.FromCollection`), so inserts, updates and deletes all change it.
   - API responses send `Cache-Control: private, no-cache` (`CacheControl.Api`): keep a copy, always
     revalidate, never `no-store` (it kills the 304 path), never `max-age`.
   - Static assets publish under content-hashed names with `Cache-Control: public, max-age=31536000,
     immutable` (`CacheControl.StaticAsset`).
   - Devices use the same validation for task lists, settings and manifests.
   `ConditionalResults.NotModifiedOr` is the one place an endpoint does this.

## Consequences

- A screen's cost is one version query per poll interval plus a 304 per client refresh; the body is produced
  only when something changed.
- Read models never go through the change tracker, so a read cannot write.
- Adding a read means one query class and one endpoint that wraps it in `NotModifiedOr`; there is no cache
  key to invent and no invalidation to forget, because the version column does both.
- A second process instance sees changes within one poll interval; the design accepts that window.
- The per-provider `IRowVersionSource` and the compare in `EntityTag` assume `row_version` is monotonic per
  table; the EF stories (E2–E5) must map it that way on both providers.
