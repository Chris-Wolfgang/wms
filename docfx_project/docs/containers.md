# Running in containers

## The images

One `Dockerfile` builds three images, each chosen with `--target`: `api`, `web` (the console) and `worker`.
They run as a non-root user, listen on port 8080 and write their logs as JSON to standard output.

```bash
docker build --target api -t wms-api .
docker build --target web -t wms-web .
docker build --target worker -t wms-worker .
```

## Worker roles

The worker image runs one role, chosen by the `WMS_ROLE` variable (in `.env`, `WMS_WORKER_ROLE`):

| Role | What it runs | Stops by itself |
|---|---|---|
| `worker` (default) | the background jobs that must run once at a time (integrity verification today, more as they arrive) | no |
| `ingest` | the file-drop and connector jobs, once they arrive | no |
| `migrate` | `wms-migrate`: applies pending database migrations, then exits | yes |

Arguments after the image name go to the role's program, so with `WMS_ROLE=migrate`,
`docker run wms-worker --status` prints the migration status.

## The compose stack

`compose.yaml` runs SQL Server 2022 Express, the API, the console, the worker and Caddy (which provides
HTTPS). Set it up once:

```bash
cp .env.example .env            # optional: the setup script creates it
pwsh scripts/compose-setup.ps1 -Hostname wms.example.com
```

The setup script:

1. generates the database passwords into `docker/secrets/` (never committed);
2. creates the `wms` database and its two logins, one for migrations and one for the running servers;
3. encrypts both connection strings with the shared key ring and writes them into `.env`;
4. runs the migrations;
5. starts the stack.

Running it again keeps the existing passwords and values.

Back up the `keys` volume with the database: it holds the key ring every server shares, and without it the
encrypted connection strings and secret settings cannot be read.

## Settings you can change

Everything is set through `.env`; nothing inside an image is edited.

| Variable | Default | Meaning |
|---|---|---|
| `WMS_HOSTNAME` | `localhost` | The host name Caddy serves and gets a certificate for |
| `WMS_HTTP_PORT`, `WMS_HTTPS_PORT` | `80`, `443` | The published ports |
| `WMS_LOG_LEVEL` | `Information` | Every server's starting log level (the runtime level is a setting) |
| `WMS_WORKER_ROLE` | `worker` | The worker service's role (`worker` or `ingest`) |
| `WMS_DB_CONNECTION` | filled in by the setup script | The servers' connection string, encrypted |
| `WMS_DB_MIGRATE_CONNECTION` | filled in by the setup script | The migration connection string, encrypted |

Any other startup key from [Configuration](configuration.md) can be added as an environment variable on
a service (for example `Wms__Hosting__AllowHttp`). Behaviour while running is changed through settings in
the console, never through a file in an image.
