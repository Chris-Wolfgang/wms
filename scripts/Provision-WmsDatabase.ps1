<#
.SYNOPSIS
    Provisions the WMS database and its two logins on SQL Server (E15.3, E15.4), or emits the script for a DBA.

.DESCRIPTION
    Creates the `wms` database (simple recovery), a migration login (the installer identity, DDL: db_owner on
    wms only, no server role) and a runtime login (the service account, data rights on the database), all
    Windows authentication — no password anywhere on this path. Idempotent: re-running keeps the logins and
    re-applies the grants. With -Script the same T-SQL is written to a file instead of run, for a DBA on the
    bring-your-own-server path (the compose stack's docker/db-init.sh is the SQL-login equivalent).

.PARAMETER Instance
    The SQL Server instance (default: the named Express instance the installer creates, `localhost\WMS`).

.PARAMETER MigrateIdentity
    The Windows account that runs migrations (default: the caller).

.PARAMETER RuntimeAccounts
    The Windows accounts the services run as (the API and the worker); each gets data rights.

.PARAMETER Database
    The database name (default wms).

.PARAMETER Script
    Write the T-SQL to this file instead of executing it.
#>
[CmdletBinding(SupportsShouldProcess)]
param
(
    [string] $Instance = 'localhost\WMS',
    [string] $MigrateIdentity = "$env:USERDOMAIN\$env:USERNAME",
    [string[]] $RuntimeAccounts = @('NT SERVICE\WolfgangWms.Api', 'NT SERVICE\WolfgangWms.Worker'),
    [string] $Database = 'wms',
    [string] $Script = ''
)

$ErrorActionPreference = 'Stop'
$RuntimeAccounts = @($RuntimeAccounts | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })   # `-File` callers pass one comma-separated string

function Quote-Identifier([string] $name) { '[' + $name.Replace(']', ']]') + ']' }
function Quote-Literal([string] $name) { "'" + $name.Replace("'", "''") + "'" }

$db = Quote-Identifier $Database
$sql = New-Object System.Text.StringBuilder
[void] $sql.AppendLine("-- Wolfgang.Wms provisioning (E15.4): database $Database, migration login $MigrateIdentity, runtime logins $($RuntimeAccounts -join ', ')")
[void] $sql.AppendLine("IF DB_ID($(Quote-Literal $Database)) IS NULL")
[void] $sql.AppendLine("BEGIN")
[void] $sql.AppendLine("    CREATE DATABASE $db;")
[void] $sql.AppendLine("    ALTER DATABASE $db SET RECOVERY SIMPLE;")
[void] $sql.AppendLine("END;")
[void] $sql.AppendLine("GO")
foreach ($login in @($MigrateIdentity) + $RuntimeAccounts)
{
    [void] $sql.AppendLine("IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = $(Quote-Literal $login))")
    [void] $sql.AppendLine("    CREATE LOGIN $(Quote-Identifier $login) FROM WINDOWS;")
}
[void] $sql.AppendLine("GO")
[void] $sql.AppendLine("USE $db;")
# A login already mapped in the database (the creator is dbo) keeps its user; roles are added to the resolved user, never to dbo.
function Add-UserToRoles([string] $login, [string[]] $roles)
{
    $lit = Quote-Literal $login
    $lines = @(
        "IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE sid = SUSER_SID($lit))",
        "    CREATE USER $(Quote-Identifier $login) FOR LOGIN $(Quote-Identifier $login);",
        "DECLARE @u_$($script:n) sysname = (SELECT name FROM sys.database_principals WHERE sid = SUSER_SID($lit));"
    )
    $lines += "DECLARE @s_$($script:n) nvarchar(400);"
    foreach ($role in $roles)
    {
        $lines += "IF @u_$($script:n) IS NOT NULL AND @u_$($script:n) <> 'dbo' BEGIN SET @s_$($script:n) = N'ALTER ROLE $role ADD MEMBER ' + QUOTENAME(@u_$($script:n)); EXEC (@s_$($script:n)); END;"
    }
    $script:n++
    return $lines
}
$script:n = 0
foreach ($line in Add-UserToRoles $MigrateIdentity @('db_owner')) { [void] $sql.AppendLine($line) }
foreach ($account in $RuntimeAccounts)
{
    foreach ($line in Add-UserToRoles $account @('db_datareader', 'db_datawriter')) { [void] $sql.AppendLine($line) }
}
[void] $sql.AppendLine("-- Grants tighten to the core/picking schemas once both exist (E15.4); the row-version sequence and triggers live there.")
[void] $sql.AppendLine("GO")
$text = $sql.ToString()

if ($Script)
{
    Set-Content -Path $Script -Value $text -Encoding utf8
    Write-Host "Provision-WmsDatabase: script written to $Script (review, then run as a sysadmin on $Instance)."
    return
}

if (-not $PSCmdlet.ShouldProcess($Instance, "provision $Database with logins $MigrateIdentity, $($RuntimeAccounts -join ', ')"))
{
    return
}

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd)
{
    throw 'sqlcmd was not found; install the SQL Server command-line tools (part of Express) or use -Script and run the file with SSMS.'
}

$file = [System.IO.Path]::GetTempFileName() + '.sql'
try
{
    Set-Content -Path $file -Value $text -Encoding utf8
    & $sqlcmd.Source -S $Instance -E -C -b -i $file
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed with exit code $LASTEXITCODE" }
    Write-Host "Provision-WmsDatabase: $Database provisioned on $Instance."
}
finally
{
    Remove-Item -Path $file -Force -ErrorAction SilentlyContinue
}
