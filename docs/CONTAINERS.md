# Containers (E14)

## Images (E14.1)

One `Dockerfile`, three runtime images built with `--target`: `api`, `web` and `worker`. The build stage
is `mcr.microsoft.com/dotnet/sdk:10.0`; the runtime images are `mcr.microsoft.com/dotnet/aspnet:10.0`
with no SDK inside, run as the non-root `app` user, listen on 8080, log JSON to standard output
(`Wms:Logging:Stdout`), and carry `curl` for the compose health checks. The publish is JIT, not trimmed:
EF Core, Serilog's sinks and the OpenID Connect stack are not trim-safe, so trimming stays with the
NativeAOT CLI and simulator (E13.3).

```bash
docker build --target api -t wms-api .
docker build --target web -t wms-web .
docker build --target worker -t wms-worker .
```

## Worker roles (E14.2)

The worker image runs one role, chosen by `WMS_ROLE`:

| Role | Runs | Exits |
|---|---|---|
| `worker` (default) | the singleton jobs under the leader lock (integrity verification today; outbox, lease expiry, rollups, retention as they arrive) | no |
| `ingest` | the ingest jobs only (file drop, connectors) once they land; today a worker that hosts no singleton job | no |
| `migrate` | `wms-migrate` with the migration login: applies pending migrations, then exits (0 ok, 1 a migration failed, 2 configuration error, 3 confirmation required) | yes |

Any arguments after the image name go to the role's program (`docker run wms-worker --status` with
`WMS_ROLE=migrate` prints the migration status).

## The compose stack (E14.3)

`compose.yaml` runs SQL Server 2022 Express, the API, the console, the worker and Caddy. Set it up once:

```bash
cp .env.example .env            # optional: scripts/compose-setup.ps1 creates it
pwsh scripts/compose-setup.ps1 -Hostname wms.example.com
```

The script generates three passwords into `docker/secrets/` (git-ignored), provisions the database
(`db-init`: the `wms` database in simple recovery, `wms_migrate` as `db_owner` on `wms` only, `wms_runtime`
with data rights, `sa` disabled), encrypts both connection strings with the shared key ring
(`wms-migrate --protect` inside the worker image; the plain strings exist only in that one container run)
and writes them into `.env` as `enc:v1:` values, runs the migrations, then starts the stack. Re-running
keeps existing secrets and values.

| Service | Image | Role |
|---|---|---|
| `db` | `mcr.microsoft.com/mssql/server:2022-latest` (`MSSQL_PID=Express`) | the database; `sa` password read from the secret file by the entrypoint wrapper, never from an environment variable |
| `db-init` | the same image, one-shot | provisioning (above); idempotent |
| `migrate` | `wms-worker`, `WMS_ROLE=migrate`, one-shot | migrations with the migration login |
| `api`, `worker`, `web` | `wms-api`, `wms-worker`, `wms-web` | the hosts, behind the proxy (`Wms:Hosting:BehindProxy=true`), sharing the `keys` volume |
| `caddy` | `caddy:2` | HTTPS for `WMS_HOSTNAME`: `/api`, `/health` and `/openapi` to the API, everything else to the console; ACME for a public name, an internal certificate for `localhost` |

Volumes: `db-data` (the database), `backups` (`/var/opt/mssql/backup`), `keys` (the Data Protection
ring every host shares — back it up with the database), `filedrop` (`/filedrop`, for the ingest role),
`caddy-data`/`caddy-config` (certificates).

Secrets: `docker/secrets/sa_password.txt`, `migrate_password.txt`, `runtime_password.txt` as Docker
secrets files, mounted only into `db` and `db-init`. The hosts never see a password: their connection
strings are `enc:v1:` values decrypted with the key ring at start (E8). `sa` is disabled after
provisioning; re-enable it through single-user mode only for a server-level change.

Smoke test (what CI runs, E13.3): `pwsh scripts/compose-setup.ps1 -NoStart`, `docker compose up -d`,
poll `https://localhost/health/ready` until 200 (the internal certificate needs `curl -k`), `docker
compose down -v`.

## Configuration (E14.4)

Everything is a variable; no file inside an image is edited.

| Variable (`.env`) | Default | Meaning |
|---|---|---|
| `WMS_HOSTNAME` | `localhost` | The hostname Caddy serves and issues a certificate for |
| `WMS_HTTP_PORT`, `WMS_HTTPS_PORT` | `80`, `443` | Published ports |
| `WMS_LOG_LEVEL` | `Information` | The boot level of every host; the runtime level is a setting (docs/LOGGING.md) |
| `WMS_WORKER_ROLE` | `worker` | The worker service's role (`worker` or `ingest`) |
| `WMS_DB_CONNECTION` | filled by the setup script | The runtime login's connection string, `enc:v1:` |
| `WMS_DB_MIGRATE_CONNECTION` | filled by the setup script | The migration login's connection string, `enc:v1:` |

Inside the containers the hosts read the usual bootstrap keys as environment variables
(`Wms__Database__Provider`, `Wms__Database__ConnectionString`, `Wms__DataProtection__KeyRingPath=/keys`,
`Wms__Hosting__BehindProxy=true`, `Wms__Logging__*`; docs/CONFIGURATION.md). Add any other bootstrap key
the same way (an `environment:` line on the service). Runtime behaviour is settings, changed in the
console or through the API, never through a file in the image.
