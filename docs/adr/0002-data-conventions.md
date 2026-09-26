# ADR 0002: Data conventions — single tenant, unit of work, repositories per aggregate, site scoping

**Status:** Accepted (E1.11) · **Date:** 2026-09-19

## Context

Each customer runs one installation with strict per-site separation: a supervisor sees only their sites and
warehouses' data never mixes. Handlers need a small, testable data seam that keeps EF Core out of the
domain and stays AOT-safe (E1.9). Reads for screens and reports have very different needs from writes.

## Decision

1. **Single tenant per install.** One company per database; no `tenant_id` anywhere. A hosted offering
   is siloed at the database: one database per customer, resolved per request from the host name or the
   device's pairing token through a resolver seam in Infrastructure, with a shared process allowed (amended
   2026-09-22 by ADR 0007; originally "one database and one Kubernetes namespace per customer"). Caches
   and worker loops are per database, never per process.
2. **Unit of work.** One `DbContext` per request or job, behind `IUnitOfWork` (`Wolfgang.Wms.Core.Data`).
   A single `SaveChangesAsync` per operation. Explicit transactions only for the listed multi-step
   operations — deposit replay, marriage, intake, settings cascade, lease release/expiry — through
   `ExecuteInTransactionAsync(Func<…>)` so rollback and EF retry are automatic, and kept free of
   non-database I/O. No distributed transactions: the outbox carries side effects. Read-committed isolation.
3. **`IUnitOfWork` is thin.** `SaveChangesAsync` and `ExecuteInTransactionAsync` only. No `Set<T>()`, no
   change-tracker access. Handlers reference repositories and `IUnitOfWork`, never `DbContext`
   (`DataAccessConventionTests`).
4. **Repository per aggregate, not per table.** A specific interface per aggregate (`ISkuRepository`,
   `IZoneGroupRepository`, `IReleaseRepository`, …) inherits the generic bases `IReadOnlyRepository<T,TId>`,
   `ISearchableRepository<T,TCriteria>`, `IWriteOnlyRepository<T>` and adds the few business-named methods
   that need EF (`GetForUpdateAsync`, `GetByBarcodeAsync`, `GetChangedSinceAsync`). Master-data CRUD comes
   from the bases for free. The bases are a candidate for a `Wolfgang.Repositories` library.
5. **Repository contracts never expose the provider.** No `IQueryable`, no `DbContext`, no expression-tree
   search: criteria objects only (AOT-safe). Enforced by `DataAccessConventionTests`.
6. **Handlers orchestrate.** Repositories fetch and persist aggregates; handlers own orchestration and
   rules; cross-aggregate work is a handler using several repositories inside one `IUnitOfWork`.
7. **Reads stay separate.** Screens and reports use query classes projecting to records
   (`AsNoTracking`), not repositories; that is where Dapper is benchmarked (E36.5). Handler unit tests use
   fakes of the repository interfaces; DbContextBuilder tests the EF repositories themselves.
8. **Site scoping.** `site_id` on every site-scoped entity with an EF global query filter from the caller's
   site context; cross-site reads require `IgnoreQueryFilters()` behind a permission; writes outside scope
   are rejected at `SaveChanges`; global tables (users, roles, global settings, SKUs, barcodes, license) are
   unfiltered; `site_id` leads most indexes.

## Consequences

- A handler's dependencies are interfaces with a handful of methods, so unit tests use hand-written fakes
  (E1.8) and never spin up a database.
- Adding an aggregate means one repository interface (a few lines) and one EF implementation in
  Infrastructure; the generic bases carry the boilerplate.
- The site filter is applied once, in Infrastructure, not remembered per query.
- Items 7 and 8 are implemented with the EF stories (E2–E5); this ADR fixes the shape they implement.
