# Configuration

Settings the installer or operator sets outside the product (everything else is a setting in the Configure
workspace, E6/E7). Each key can be set in `appsettings.json`, `appsettings.<Environment>.json`, or as an
environment variable with `__` for `:` (`Wms__Database__Provider`). Environment variables win.

## Only bootstrap keys (E6.5)

`appsettings` holds what the process needs before it can reach the database, and nothing else:

| Key | Story |
|-----|-------|
| `Wms:Database:Provider`, `ConnectionString`, `AutoMigrate`, `TrustServerCertificate` | E2, below |
| `Wms:DataProtection:KeyRingPath` | E8.1, below |
| `Wms:Bootstrap:AdminUserName` | E9.1, docs/AUTH.md: the bootstrap administrator's name (default `admin`); read once |
| `Urls`, `Kestrel:*`, `AllowedHosts` | ASP.NET Core hosting |
| `Wms:Hosting:BehindProxy` | E10.6, below |
| `Wms:Hosting:AllowHttp` | E12.5, below: plain HTTP from any address (a lab) |
| `Wms:Auth:ForceLocal` | E11.0, docs/AUTH.md: emergency override, local sign-in only; not the normal way to choose providers |
| `Logging:*` | log levels; runtime control arrives with E12.4 |

Any other key in an `appsettings*.json` file is ignored, and the host logs one warning at startup naming each
one with its file (`Smtp:Host (appsettings.json)`), so a value typed into the wrong place is noticed. Those
values are settings: define them in a module (docs/SETTINGS.md) and edit them in the Configure workspace.
Environment variables are not inspected (the platform sets its own).

## Database (E2)

| Key | Values | Notes |
|-----|--------|-------|
| `Wms:Database:Provider` | `SqlServer`, `PostgreSql`, `None` | Chosen at install time. `None` starts the host without a database for bootstrap only (`GET /system/schema`, health); it is the shipped default so a fresh install can be probed before it is configured. Anything else fails startup: `Wms:Database:Provider must be one of SqlServer, PostgreSql or None; got 'Oracle'.` |
| `Wms:Database:ConnectionString` | provider connection string, plain or `enc:v1:…` | Required for `SqlServer` and `PostgreSql`; startup fails when missing. Store it encrypted (`wms-migrate --protect`, E8.2) or supply it from an environment variable / container secret (E8.4); a plain string is accepted for development. |
| `Wms:Database:AutoMigrate` | `true` / `false` (default) | Apply pending migrations when the API starts instead of refusing to start (E4.4). **Bundled installs only** (the installer's own database, one process): everywhere else run `wms-migrate` as a separate step with the DBA's rights and leave this off. |
| `Wms:Database:TrustServerCertificate` | `true` / `false` (default) | SQL Server only. Trusts the server certificate without validating its chain, which SQL Server Express and self-signed development servers need. Never on a shared network: install a certificate instead. Setting it with `PostgreSql` fails startup. |

Examples:

```json
{ "Wms": { "Database": { "Provider": "SqlServer", "ConnectionString": "Server=db;Database=wms;User Id=wms;Password=…;Encrypt=True", "TrustServerCertificate": true } } }
```

```json
{ "Wms": { "Database": { "Provider": "PostgreSql", "ConnectionString": "Host=db;Database=wms;Username=wms;Password=…" } } }
```

Supported engines: SQL Server 2022 and later including Express (E2.2), PostgreSQL 16 and later (E2.3). The
model is shared; migrations are generated per provider (E2.4) and applied by `wms migrate`, never by the API
(docs/BOOTSTRAP.md).

## Secrets and the key ring (E8)

| Key | Values | Notes |
|-----|--------|-------|
| `Wms:DataProtection:KeyRingPath` | directory | Where the Data Protection key ring lives when a directory is wanted (single-node installs, and required for an encrypted connection string). Created on first run with access for the running user only; back it up and mount it into containers. **Without it the ring is stored in the database** (`wms.data_protection_key`, E8.6), the default, so several instances share one ring with no shared volume. |

**Encrypted connection string (E8.2).** `wms-migrate --protect --connection-string "<plain>" --key-ring <path>`
prints the string as `enc:v1:…`; put that in `appsettings.json` or the environment variable instead of the
plain text. The hosts and the tool decrypt it with the ring at `KeyRingPath`; a plain string still works
(development). An encrypted connection string needs the **file** ring: the database cannot be opened before
the string is decrypted, so startup fails with a clear message when `KeyRingPath` is missing or the ring does
not hold the key. Every instance that shares the encrypted string must share the same ring (a mounted
volume).

**Environment variables (E8.4).** `Wms__Database__ConnectionString` and `Wms__DataProtection__KeyRingPath`
override the file values, so a container secret store can inject them; the hosts read variables after
`appsettings*.json`.

**One ring for all instances (E8.6).** Every instance of the API and the worker must use the same key ring,
or a value one instance encrypted is unreadable to another: with the database ring that is automatic; with
a directory ring every instance mounts the same directory. Rotating or losing the ring makes every stored
secret unreadable; back it up with the database. Before the database exists (bootstrap, `Provider` =
`None`) the framework default ring is used and nothing durable is encrypted.

**Secret settings (E8.3).** A setting of kind `Secret` is stored encrypted in `core.setting` (`enc:v1:`)
through the same protector, decrypted only for the typed read, and masked in every API response and page;
the console offers "replace" rather than showing it. A settings export (later) omits secrets unless asked to
include them.

**Corporate vaults (E8.5).** Every secret the product encrypts or decrypts goes through one interface,
`ISecretProtector` (`Wolfgang.Wms.Core.Secrets`): `Protect(plain)` → `enc:v1:…`, `Unprotect(enc)` → plain.
The default implementation is Data Protection over the configured ring; a customer whose security team owns
credentials registers their own implementation before `AddWmsDataProtection` and the product uses it for the
connection string, secret settings (E8.3) and everything after. Implementations never log plain text.

## Reverse proxies (E10.6)

| Key | Values | Notes |
|-----|--------|-------|
| `Wms:Hosting:BehindProxy` | `true` / `false` (default) | Set to `true` when Caddy, IIS or an ingress terminates TLS in front of the API and the console. The hosts then take the scheme, host and client address from `X-Forwarded-Proto`, `X-Forwarded-Host` and `X-Forwarded-For`, so session cookies are marked secure, HTTPS checks pass and rate limits see real addresses. The proxy must be the only way in and must strip those headers from clients. Left `false`, the headers are ignored. |

Browser apps on other origins are allowed by the `api.cors.allowed_origins` setting (comma-separated
origins, no path), applied on the next request; nothing is allowed until an origin is listed.

## Health probes (E12.1)

| Route | Answers | Use |
|---|---|---|
| `GET /health/live` | 200 while the process serves requests; no checks | liveness probe, load-balancer ping |
| `GET /health/ready` | 200 when every readiness check passes, 503 otherwise | readiness probe; take the instance out of rotation |

Both are on the host root (not under `/api/v<n>`), anonymous, answer JSON (`status`, `totalDuration`,
`checks[]` with `name`, `status`, `description`, `duration`) with `Cache-Control: no-store`, and accept
plain HTTP. Readiness fails when the database cannot be reached, is behind this build (pending migrations)
or ahead of it; the description names the schema version or the migrations involved. A host without a
database configured has no readiness checks and answers 200.

## HTTPS (E12.5)

The API refuses plain HTTP with `400 hosting.https_required` rather than redirecting, so a POST body is
never lost to a redirect. Exempt: requests from the loopback address (a developer, a local probe), the
health probes, and `Wms:Hosting:AllowHttp=true` (a lab; never production). Behind a reverse proxy set
`Wms:Hosting:BehindProxy` so the forwarded scheme counts. The console redirects to HTTPS instead
(`UseHttpsRedirection`, HSTS outside Development); when the proxy terminates TLS, redirect there and keep
the console's redirect as a backstop.

## Several instances (E12.6)

The API is stateless: sessions are encrypted cookies on the shared Data Protection ring (E8), settings
and provider selection are re-read from the database on every instance, and no request depends on the
instance that served the last one. Run as many API instances as you like behind the proxy.

Singleton worker jobs (integrity verification now; outbox, lease expiry, rollups, retention and ingest
as they arrive) run under a database leader lock: `wms.leader_lock` holds one row per job with the holder
and a lease; the holder renews every third of the lease, another worker takes over once the lease has
expired without renewal, and a lease that cannot be renewed cancels the job's `Lost` token. Run as many
workers as you like; exactly one executes each job.

EF Core retries transient faults (E6.4) and transactions are kept to one save. The console in Server
render mode keeps a circuit per browser tab: behind a load balancer it needs sticky sessions (a few dozen
supervisors is the expected load; the API is the tier that scales).
