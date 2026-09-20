#!/bin/bash
# E14.3 / E15.4 (compose path): provisions the wms database with two logins from the secret files —
# wms_migrate (db_owner on wms only, no server role; used by the migrate role) and wms_runtime (data
# rights; used by the API and the worker) — then disables sa. Same end state as the Windows installer.
# Idempotent: logins are created once, passwords and grants re-applied on every run.
set -euo pipefail

sa_password="$(cat /run/secrets/sa_password)"
migrate_password="$(cat /run/secrets/migrate_password)"
runtime_password="$(cat /run/secrets/runtime_password)"
sqlcmd() { /opt/mssql-tools18/bin/sqlcmd -S db -U sa -P "$sa_password" -C -b "$@"; }

for attempt in $(seq 1 30); do
  # Provisioned already (sa is disabled afterwards): the migration login reaches the database. Nothing to do.
  if /opt/mssql-tools18/bin/sqlcmd -S db -d wms -U wms_migrate -P "$migrate_password" -C -b -Q "SELECT 1" >/dev/null 2>&1; then
    echo "db-init: wms already provisioned; nothing to do."
    exit 0
  fi
  if sqlcmd -Q "SELECT 1" >/dev/null 2>&1; then break; fi
  echo "db-init: waiting for SQL Server ($attempt)"; sleep 2
done

sqlcmd -v migrate_password="$migrate_password" runtime_password="$runtime_password" <<'SQL'
IF DB_ID('wms') IS NULL
BEGIN
    CREATE DATABASE wms;
    ALTER DATABASE wms SET RECOVERY SIMPLE;
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'wms_migrate')
    CREATE LOGIN wms_migrate WITH PASSWORD = '$(migrate_password)', CHECK_POLICY = ON;
ELSE
    ALTER LOGIN wms_migrate WITH PASSWORD = '$(migrate_password)';
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'wms_runtime')
    CREATE LOGIN wms_runtime WITH PASSWORD = '$(runtime_password)', CHECK_POLICY = ON;
ELSE
    ALTER LOGIN wms_runtime WITH PASSWORD = '$(runtime_password)';
GO
USE wms;
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'wms_migrate')
    CREATE USER wms_migrate FOR LOGIN wms_migrate;
ALTER ROLE db_owner ADD MEMBER wms_migrate;
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'wms_runtime')
    CREATE USER wms_runtime FOR LOGIN wms_runtime;
ALTER ROLE db_datareader ADD MEMBER wms_runtime;
ALTER ROLE db_datawriter ADD MEMBER wms_runtime;
GO
-- The runtime login needs the row-version sequence and the triggers' SET CONTEXT_INFO; both live in the
-- schemas the migrations create. Grants tighten to the core/picking schemas once both exist (E15.4).
USE master;
ALTER LOGIN sa DISABLE;
GO
SQL

echo "db-init: wms database provisioned; sa disabled."
