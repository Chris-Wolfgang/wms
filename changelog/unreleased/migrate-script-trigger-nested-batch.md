type: fix

The SQL Server script `wms-migrate --script` generates is valid for versioned tables: the row-version update trigger is created as a nested batch (`EXEC(N'CREATE OR ALTER TRIGGER ...')`), which the idempotent `IF NOT EXISTS ... BEGIN ... END` wrapper allows, where a bare `CREATE TRIGGER` failed with "Incorrect syntax near the keyword 'OR'".
