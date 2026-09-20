type: feature

Device sync reads for every synced master table: `ISoftDeletable`/`ISyncedEntity` with a default query filter hiding deleted rows, `SyncQueries` (deltas since a watermark with a safety margin, manifests of live rows) and `MapSyncedTable` generating `GET {table}?since=&size=` and `GET {table}/manifest`; versioned entities now declare their row-version trigger so SQL Server saves work.
