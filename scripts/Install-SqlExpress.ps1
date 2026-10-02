<#
.SYNOPSIS
    Installs SQL Server Express as the `WMS` named instance and provisions the WMS database (E15.4).

.DESCRIPTION
    Downloads Microsoft's official Express bootstrapper at install time (or uses a pre-downloaded package on
    an air-gapped machine), runs Microsoft's setup — the customer accepts Microsoft's license terms in
    Microsoft's own dialog; Express is never bundled or redistributed — with the WMS instance pre-configured:
    named instance `WMS`, Windows authentication only, TCP enabled, the caller as sysadmin. Afterwards TCP is
    restricted to localhost and scripts/Provision-WmsDatabase.ps1 creates the database and the two Windows
    logins, so the services start with an integrated-auth connection string and no secret in configuration.

.PARAMETER Package
    A pre-downloaded Express bootstrapper or setup.exe (air-gapped option). When omitted the bootstrapper is
    downloaded from -BootstrapperUrl.

.PARAMETER BootstrapperUrl
    Microsoft's download link for the Express bootstrapper (default: SQL Server 2022 Express; pass the 2025
    link once Microsoft publishes it).

.PARAMETER InstanceName
    The named instance (default WMS).

.PARAMETER RuntimeAccounts
    The service accounts that get data rights (passed to Provision-WmsDatabase.ps1).

.PARAMETER SkipProvision
    Install only; provision later (or with -Script through Provision-WmsDatabase.ps1).
#>
[CmdletBinding(SupportsShouldProcess)]
param
(
    [string] $Package = '',
    [string] $BootstrapperUrl = 'https://go.microsoft.com/fwlink/p/?linkid=2216019',
    [string] $InstanceName = 'WMS',
    [string[]] $RuntimeAccounts = @('NT SERVICE\WolfgangWms.Api', 'NT SERVICE\WolfgangWms.Worker'),
    [switch] $SkipProvision
)

$ErrorActionPreference = 'Stop'
$work = Join-Path $env:TEMP 'wms-sqlexpress'
New-Item -ItemType Directory -Force -Path $work | Out-Null

if (-not $Package)
{
    $Package = Join-Path $work 'SQLExpress-bootstrapper.exe'
    if ($PSCmdlet.ShouldProcess($BootstrapperUrl, "download the Express bootstrapper to $Package"))
    {
        Invoke-WebRequest -Uri $BootstrapperUrl -OutFile $Package -UseBasicParsing
    }
}

# Microsoft's setup: the instance is pre-configured; the license terms are shown and accepted in Microsoft's
# dialog (no /IACCEPTSQLSERVERLICENSETERMS here, on purpose). Windows authentication only (no SECURITYMODE),
# TCP on for the hosts, the caller as sysadmin so provisioning can run right after.
$caller = "$env:USERDOMAIN\$env:USERNAME"
$arguments = @(
    '/ACTION=Install',
    '/FEATURES=SQLEngine',
    "/INSTANCENAME=$InstanceName",
    '/TCPENABLED=1',
    "/SQLSYSADMINACCOUNTS=""$caller""",
    '/SQLSVCSTARTUPTYPE=Automatic'
)
if ($PSCmdlet.ShouldProcess($Package, "run Microsoft's setup: $($arguments -join ' ')"))
{
    $process = Start-Process -FilePath $Package -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -ne 0 -and $process.ExitCode -ne 3010)
    {
        throw "SQL Server Express setup exited with $($process.ExitCode); see %ProgramFiles%\Microsoft SQL Server\*\Setup Bootstrap\Log\Summary.txt"
    }
}

# TCP on localhost only: every listener except the loopback addresses is disabled, and the dynamic port is
# kept; the hosts connect as localhost\WMS through the SQL Browser (or a fixed port a DBA may set later).
$instanceKey = Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server' -ErrorAction SilentlyContinue |
    Where-Object { $_.PSChildName -like "MSSQL*.$InstanceName" } | Select-Object -First 1
if ($instanceKey -and $PSCmdlet.ShouldProcess("$($instanceKey.PSChildName) TCP listeners", 'restrict to localhost'))
{
    $tcp = Join-Path $instanceKey.PSPath 'MSSQLServer\SuperSocketNetLib\Tcp'
    foreach ($ip in Get-ChildItem $tcp | Where-Object { $_.PSChildName -like 'IP*' -and $_.PSChildName -ne 'IPAll' })
    {
        $address = (Get-ItemProperty $ip.PSPath).IpAddress
        $loopback = $address -in @('127.0.0.1', '::1')
        Set-ItemProperty -Path $ip.PSPath -Name Enabled -Value ([int] $loopback)
        Set-ItemProperty -Path $ip.PSPath -Name Active -Value 1
    }

    Restart-Service "MSSQL`$$InstanceName" -Force
}

if ($SkipProvision)
{
    Write-Host "Install-SqlExpress: instance $InstanceName installed; run scripts/Provision-WmsDatabase.ps1 next."
    return
}

& (Join-Path $PSScriptRoot 'Provision-WmsDatabase.ps1') -Instance "localhost\$InstanceName" -MigrateIdentity $caller -RuntimeAccounts $RuntimeAccounts -WhatIf:$WhatIfPreference
Write-Host "Install-SqlExpress: done. Connection string for the services: Server=localhost\$InstanceName;Database=wms;Integrated Security=true;Encrypt=True;TrustServerCertificate=True"
