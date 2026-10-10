<#
.SYNOPSIS
    Sets up the compose stack once (E14.3): passwords, database logins, encrypted connection strings, migrations.

.DESCRIPTION
    1. Generates three passwords into docker/secrets/ (sa, wms_migrate, wms_runtime) unless they exist.
    2. Creates .env from .env.example unless it exists.
    3. Starts SQL Server and runs db-init (database, logins, sa disabled).
    4. Encrypts the two connection strings with the shared key ring (wms-migrate --protect inside the worker
       image) and writes them into .env — the plain strings exist only inside that one container run.
    5. Runs the migrate role, then starts the whole stack.
    Re-running is safe: existing secrets and .env values are kept.

.PARAMETER Hostname
    The hostname Caddy serves; written to .env when it is created.

.PARAMETER NoStart
    Stop after the migrations (for CI smoke tests that start the stack themselves).
#>
[CmdletBinding()]
param
(
    [string] $Hostname = 'localhost',
    [switch] $NoStart
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root

function New-Password
{
    # 32 characters from a set SQL Server's policy accepts; no shell-special characters.
    $alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    $bytes = [byte[]]::new(32)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return 'Wms' + (-join ($bytes | ForEach-Object { $alphabet[$_ % $alphabet.Length] })) + '!'
}

$secrets = Join-Path $root 'docker/secrets'
New-Item -ItemType Directory -Force -Path $secrets | Out-Null
foreach ($name in 'sa_password', 'migrate_password', 'runtime_password')
{
    $file = Join-Path $secrets "$name.txt"
    if (-not (Test-Path $file))
    {
        [System.IO.File]::WriteAllText($file, (New-Password))
        Write-Host "compose-setup: generated $name"
    }
}

$env = Join-Path $root '.env'
if (-not (Test-Path $env))
{
    (Get-Content (Join-Path $root '.env.example')) -replace '^WMS_HOSTNAME=.*$', "WMS_HOSTNAME=$Hostname" | Set-Content $env
    Write-Host "compose-setup: created .env for $Hostname"
}

Write-Host 'compose-setup: building the worker image'
docker compose build worker
if ($LASTEXITCODE -ne 0) { throw 'docker compose build failed' }

Write-Host 'compose-setup: starting SQL Server and provisioning the database'
docker compose up -d db
docker compose run --rm db-init
if ($LASTEXITCODE -ne 0) { throw 'db-init failed' }

function Protect-ConnectionString([string] $login, [string] $passwordFile)
{
    $password = [System.IO.File]::ReadAllText($passwordFile).Trim()
    $plain = "Server=db;Database=wms;User Id=$login;Password=$password;Encrypt=True;TrustServerCertificate=True"
    $output = docker compose run --rm --no-deps -e WMS_ROLE=migrate migrate --protect --key-ring /keys --connection-string $plain 2>&1
    if ($LASTEXITCODE -ne 0) { throw "wms-migrate --protect failed for ${login}: $output" }
    $line = ($output | Where-Object { $_ -match '^enc:v1:' } | Select-Object -Last 1)
    if (-not $line) { throw "wms-migrate --protect printed no enc:v1: string for ${login}: $output" }
    return $line.Trim()
}

Write-Host 'compose-setup: encrypting the connection strings with the shared key ring'
$runtime = Protect-ConnectionString 'wms_runtime' (Join-Path $secrets 'runtime_password.txt')
$migrate = Protect-ConnectionString 'wms_migrate' (Join-Path $secrets 'migrate_password.txt')
$envText = Get-Content $env -Raw
$envText = $envText -replace '(?m)^WMS_DB_CONNECTION=.*$', "WMS_DB_CONNECTION=$runtime"
$envText = $envText -replace '(?m)^WMS_DB_MIGRATE_CONNECTION=.*$', "WMS_DB_MIGRATE_CONNECTION=$migrate"
Set-Content $env $envText

Write-Host 'compose-setup: applying migrations'
docker compose run --rm migrate
if ($LASTEXITCODE -ne 0) { throw 'migrate failed' }

if ($NoStart)
{
    Write-Host 'compose-setup: done (stack not started).'
    return
}

Write-Host 'compose-setup: starting the stack'
docker compose up -d
Write-Host "compose-setup: done. Console: https://$Hostname/  API: https://$Hostname/api/v0/  Ready: https://$Hostname/health/ready"
