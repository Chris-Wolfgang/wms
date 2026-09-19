type: feature

Add the data-access seam (`IUnitOfWork`, `IReadOnlyRepository<T,TId>`, `ISearchableRepository<T,TCriteria>`, `IWriteOnlyRepository<T>`) with ADR 0002 and a convention test that keeps `IQueryable`, expression trees and contexts out of contracts and handlers.
