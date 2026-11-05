# Installing on Windows (E15)

Three hosts — the API, the console and the worker — run as Windows services, or the API and the console
under IIS with the worker still a service. SQL Server Express (or any SQL Server) holds the database; the
services connect with Windows authentication by default, so no secret sits in configuration.

## 1. SQL Server Express (E15.4)

```powershell
pwsh scripts/Install-SqlExpress.ps1                       # downloads Microsoft's bootstrapper, runs Microsoft's setup
pwsh scripts/Install-SqlExpress.ps1 -Package D:\SQLEXPR_x64_ENU.exe   # air-gapped: a pre-downloaded package
```

The script downloads the official Express bootstrapper from Microsoft at install time (`-BootstrapperUrl`,
default SQL Server 2022 Express; pass the 2025 link when Microsoft publishes it) and runs Microsoft's setup
with the WMS instance pre-configured: named instance `WMS`, Windows authentication only, TCP enabled, the
installing account as sysadmin. The customer accepts Microsoft's license terms in Microsoft's own dialog;
Express is never bundled or redistributed. Afterwards TCP is restricted to the loopback addresses and the
instance restarted, then the database is provisioned (below). Bring your own SQL Server instead: skip this
step and run the provisioning script (or hand its `-Script` output to your DBA).

### Provisioning (`scripts/Provision-WmsDatabase.ps1`)

Creates the `wms` database (simple recovery) and two Windows logins: the **migration login** (the
installing identity; `db_owner` on `wms` only, no server role) and the **runtime login(s)** (the service
accounts; data rights). Idempotent. `-Script provision.sql` writes the same T-SQL for a DBA on the
bring-your-own path instead of running it. The compose stack's `docker/db-init.sh` is the SQL-login
equivalent (a random, temporary `sa`; `sa` disabled at the end).

Left for a follow-up in this story: schema-level grants for the runtime login once the second schema
exists, the local certificate for encrypted connections without `TrustServerCertificate`, and
`wms provision --reset-runtime-password` (SQL-login path) once the CLI exists.

## 2. Publish the hosts

```powershell
dotnet publish src/Wolfgang.Wms.Api    -c Release -o publish\api
dotnet publish src/Wolfgang.Wms.Web    -c Release -o publish\web
dotnet publish src/Wolfgang.Wms.Worker -c Release -o publish\worker
dotnet publish src/Wolfgang.Wms.Migrate -c Release -o publish\worker\migrate
```

## 3. Windows services (E15.1, E15.3)

```powershell
pwsh scripts/Install-WindowsServices.ps1 -PublishRoot .\publish                                    # virtual accounts, integrated auth
pwsh scripts/Install-WindowsServices.ps1 -PublishRoot .\publish -Credential (Get-Credential CORP\svc-wms)   # one chosen account
pwsh scripts/Install-WindowsServices.ps1 -PublishRoot .\publish -Authentication Sql -ConnectionString "Server=db1;Database=wms;User Id=wms_runtime;Password=..."
pwsh scripts/Install-WindowsServices.ps1 -Uninstall
```

The installer copies the hosts under `C:\Program Files\Wolfgang.Wms`, creates the key ring
(`C:\ProgramData\Wolfgang.Wms\keys`, shared by the three services) and the log directory, registers
`WolfgangWms.Api`, `WolfgangWms.Web` and `WolfgangWms.Worker` (automatic start, restart on failure) as the
chosen account — per-service virtual accounts `NT SERVICE\WolfgangWms.<Host>` by default (no password to
manage), or one account given with `-Credential` — grants that account the key ring and log directories,
configures each service through its own environment (the same `Wms__*` keys the containers use; nothing in
`appsettings.json` is edited), registers the `Wolfgang.Wms` event source, runs the migrations with the
caller's identity (the migration login), starts the services, waits for `/health/ready`, and stops and
restarts the API once to verify the service controller round trip. `-WhatIf` prints every step.

**Authentication** (E15.3): `-Authentication Integrated` (default) builds
`Server=localhost\WMS;Database=wms;Integrated Security=true;…` — the service account is the runtime login,
no secret anywhere. `-Authentication Sql` takes a full connection string with a SQL login and stores it
encrypted with the key ring (`enc:v1:`, exactly what `wms-migrate --protect` prints), matching a DBA policy
that forbids Windows logins.

Logs go to `C:\ProgramData\Wolfgang.Wms\logs\<host>-<date>.log` (JSON lines) and, Warning and above, to the
Application event log under the `Wolfgang.Wms` source (docs/LOGGING.md). The runtime log level is a setting.

Kestrel listens on `-ApiUrl` / `-WebUrl` (defaults `https://localhost:5001` and `https://localhost:5002`,
the ASP.NET development certificate); for a hostname put IIS, Caddy or another reverse proxy in front and
set `Wms__Hosting__BehindProxy=true` (docs/CONFIGURATION.md).

## 4. IIS instead of Kestrel for the API and the console (E15.2)

Both web hosts publish with `AspNetCoreHostingModel=InProcess`: `dotnet publish` writes a `web.config`
that IIS's ASP.NET Core Module loads in-process. Install the .NET 10 Hosting Bundle, create one site (or
application) per host pointing at its publish folder, run the application pool as the account that holds
the runtime login (integrated authentication) with "Load User Profile" on, and set the environment
(`Wms__Database__*`, `Wms__DataProtection__KeyRingPath`, `Wms__Hosting__BehindProxy=true`,
`Wms__Logging__*`) on the site through IIS's configuration editor
(`system.webServer/aspNetCore/environmentVariables`), not in `appsettings.json`. IIS terminates TLS;
bindings carry the hostname and certificate. The worker has no web surface and remains a Windows service
(step 3 with only the worker copied, or the full installer with the API and console services stopped and
disabled).

## What comes next

- An MSI (WiX) wrapping these scripts with a dialog for the account, the authentication choice and the
  Express option (E15.1 "installer" in its final form); the scripts stay the source of truth.
- Certificate provisioning for encrypted SQL connections without `TrustServerCertificate` (E15.4).
