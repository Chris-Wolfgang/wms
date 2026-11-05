type: feature

Typed settings accessor: `ISettings.Get<T>`/`Set<T>`/`Reset` validate against the registry, store invariant text, cascade effective values to descendants that inherit, and serve reads from a per-instance cache invalidated by `row_version` (`MaxRowVersionSource`); `GET/PUT/DELETE /settings/{scope}/{id}/{key}` with `If-Match` on existing rows; a defaults-only accessor answers before a database is configured.
