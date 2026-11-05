type: fix

The SQL Server row-version trigger no longer re-fires itself when the database option `RECURSIVE_TRIGGERS` is on, and on PostgreSQL dropping the last row-version trigger also drops the shared `wms.set_row_version()` function, so a full downgrade leaves nothing behind.
