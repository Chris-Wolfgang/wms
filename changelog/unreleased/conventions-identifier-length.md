type: fix

The model conventions verifier reports any table, column, key, constraint or index name longer than 63 characters on both providers, so a convention-built name is never silently truncated on PostgreSQL while SQL Server keeps it; the remedy is a shorter explicit name in the module's configuration.
