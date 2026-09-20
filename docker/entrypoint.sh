#!/bin/bash
# E14.2: the worker image runs one of three roles, chosen by WMS_ROLE.
#   worker   the singleton jobs (verification, and every job that arrives with its epic) under the leader lock
#   ingest   the ingest jobs only (file drop, connectors) once they land; today a worker that hosts no singleton job
#   migrate  applies pending migrations with the migration login and exits (0 ok, 1 failed, 2 configuration)
set -euo pipefail

role="${WMS_ROLE:-worker}"
case "$role" in
  worker|ingest)
    export Wms__Worker__Role="$role"
    exec dotnet /app/Wolfgang.Wms.Worker.dll "$@"
    ;;
  migrate)
    cd /app/migrate
    exec dotnet /app/migrate/wms-migrate.dll "$@"
    ;;
  *)
    echo "WMS_ROLE must be worker, ingest or migrate (got '$role')." >&2
    exit 2
    ;;
esac
