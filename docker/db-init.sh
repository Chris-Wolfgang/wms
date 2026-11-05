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

# Every postcondition of a finished run, checked as wms_migrate (db_owner on wms; sa is visible to any login as a
# system login): the runtime user with its two roles, the migration user as owner, and sa disabled.
complete_query="SET NOCOUNT ON;
SELECT CASE WHEN DATABASE_PRINCIPAL_ID('wms_runtime') IS NOT NULL
  AND IS_ROLEMEMBER('db_datareader', 'wms_runtime') = 1 AND IS_ROLEMEMBER('db_datawriter', 'wms_runtime') = 1
  AND IS_ROLEMEMBER('db_owner', 'wms_migrate') = 1
  AND (SELECT is_disabled FROM sys.server_principals WHERE name = 'sa') = 1 THEN 'complete' ELSE 'incomplete' END;"
migrate_query() { /opt/mssql-tools18/bin/sqlcmd -S db -d wms -U wms_migrate -P "$migrate_password" -C -b -h -1 -Q "$1" 2>/dev/null; }

for attempt in $(seq 1 30); do
  # A finished run leaves sa disabled, so the migration login is how a re-run looks; only a complete run is skipped.
  if state="$(migrate_query "$complete_query")"; then
    if [[ "$state" == *complete* && "$state" != *incomplete* ]]; then
      echo "db-init: wms already provisioned; nothing to do."
      exit 0
    fi
    if ! sqlcmd -Q "SELECT 1" >/dev/null 2>&1; then
      echo "db-init: an earlier run stopped partway and sa is no longer usable; finish provisioning by hand (docs/CONTAINERS.md)." >&2
      exit 1
    fi
    echo "db-init: an earlier run stopped partway; provisioning again."
    break
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
-- The sa password from the secret file was in SQL Server's environment and is on the host's disk. Replace it with
-- a random value generated here and never stored, then disable sa: every copy of the original becomes worthless,
-- and whoever re-enables sa (single-user mode) must set a password of their own.
DECLARE @unknown nvarchar(128) = REPLACE(CONVERT(nvarchar(36), NEWID()) + CONVERT(nvarchar(36), NEWID()), N'-', N'') + N'a#';
DECLARE @rotate nvarchar(max) = N'ALTER LOGIN sa WITH PASSWORD = ' + QUOTENAME(@unknown, '''');
EXEC (@rotate);
ALTER LOGIN sa DISABLE;
GO
SQL

echo "db-init: wms database provisioned; sa given an unknown password and disabled."
