<#
.SYNOPSIS
    Installs (or removes) the API, the console and the worker as Windows services (E15.1, E15.3).

.DESCRIPTION
    Copies the published hosts under -InstallRoot, creates the key ring and log directories with rights for
    the service accounts, configures each service through its own environment (the same Wms__* keys the
    containers use; nothing in appsettings.json is edited), registers the three services with the chosen
    account (virtual accounts `NT SERVICE\WolfgangWms.<Host>` by default, or one account given with
    -Credential), runs the migrations with the caller's identity, starts the services and verifies
    /health/ready. -Uninstall stops and removes the services and leaves the data (key ring, logs) in place.

    Authentication (E15.3): -Authentication Integrated builds the connection string on the service account
    (Windows authentication; no secret in configuration); -Authentication Sql takes -ConnectionString with a
    SQL login and stores it encrypted with the key ring (enc:v1:, as wms-migrate --protect prints).

.PARAMETER PublishRoot
    The directory holding the published hosts: api\, web\, worker\ (each with wms-migrate next to the worker).

.PARAMETER InstallRoot
    Where the hosts are installed (default C:\Program Files\Wolfgang.Wms).

.PARAMETER DataRoot
    Where the key ring and logs live (default C:\ProgramData\Wolfgang.Wms).

.PARAMETER Credential
    The account every service runs as; omitted, each service runs as its virtual account.

.PARAMETER Authentication
    Integrated (default) or Sql.

.PARAMETER Server
    The SQL Server instance for Integrated authentication (default localhost\WMS).

.PARAMETER ConnectionString
    The full connection string for Sql authentication (encrypted before it is stored).

.PARAMETER ApiUrl, WebUrl
    Kestrel URLs of the API and the console (defaults https://localhost:5001 and https://localhost:5002; put a
    reverse proxy or IIS in front for a hostname).

.PARAMETER Uninstall
    Stop and remove the services.
#>
[CmdletBinding(SupportsShouldProcess)]
param
(
    [string] $PublishRoot = '',
    [string] $InstallRoot = 'C:\Program Files\Wolfgang.Wms',
    [string] $DataRoot = 'C:\ProgramData\Wolfgang.Wms',
    [System.Management.Automation.PSCredential] $Credential = $null,
    [ValidateSet('Integrated', 'Sql')] [string] $Authentication = 'Integrated',
    [string] $Server = 'localhost\WMS',
    [string] $ConnectionString = '',
    [string] $ApiUrl = 'https://localhost:5001',
    [string] $WebUrl = 'https://localhost:5002',
    [switch] $Uninstall
)

$ErrorActionPreference = 'Stop'
$services = @(
    @{ Name = 'WolfgangWms.Api';    Folder = 'api';    Exe = 'Wolfgang.Wms.Api.exe';    Url = $ApiUrl; Description = 'Wolfgang.Wms API' },
    @{ Name = 'WolfgangWms.Web';    Folder = 'web';    Exe = 'Wolfgang.Wms.Web.exe';    Url = $WebUrl; Description = 'Wolfgang.Wms console' },
    @{ Name = 'WolfgangWms.Worker'; Folder = 'worker'; Exe = 'Wolfgang.Wms.Worker.exe'; Url = '';      Description = 'Wolfgang.Wms worker (singleton jobs)' }
)

function Get-Account([hashtable] $service)
{
    if ($Credential) { return $Credential.UserName }
    return "NT SERVICE\$($service.Name)"
}

if ($Uninstall)
{
    foreach ($service in $services)
    {
        $existing = Get-Service -Name $service.Name -ErrorAction SilentlyContinue
        if (-not $existing) { continue }
        if ($PSCmdlet.ShouldProcess($service.Name, 'stop and remove the service'))
        {
            if ($existing.Status -ne 'Stopped') { Stop-Service -Name $service.Name -Force }
            & sc.exe delete $service.Name | Out-Null
        }
    }

    Write-Host "Install-WindowsServices: services removed; $InstallRoot and $DataRoot left in place."
    return
}

if (-not $PublishRoot) { throw 'PublishRoot is required: the directory with the published api\, web\ and worker\ hosts.' }
foreach ($service in $services)
{
    if (-not (Test-Path (Join-Path $PublishRoot "$($service.Folder)\$($service.Exe)"))) { throw "Missing $($service.Folder)\$($service.Exe) under $PublishRoot" }
}

$keyRing = Join-Path $DataRoot 'keys'
$logs = Join-Path $DataRoot 'logs'
if ($PSCmdlet.ShouldProcess($DataRoot, 'create the key ring and log directories'))
{
    New-Item -ItemType Directory -Force -Path $keyRing, $logs | Out-Null
}

# 1. Files.
foreach ($service in $services)
{
    $target = Join-Path $InstallRoot $service.Folder
    if ($PSCmdlet.ShouldProcess($target, "copy the published $($service.Folder) host"))
    {
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        Copy-Item -Path (Join-Path $PublishRoot "$($service.Folder)\*") -Destination $target -Recurse -Force
    }
}

# 2. The connection string: integrated on the service account, or a SQL login encrypted with the key ring.
$migrateExe = Join-Path $InstallRoot 'worker\migrate\wms-migrate.exe'
if (-not (Test-Path $migrateExe)) { $migrateExe = Join-Path $InstallRoot 'worker\wms-migrate.exe' }
switch ($Authentication)
{
    'Integrated' { $stored = "Server=$Server;Database=wms;Integrated Security=true;Encrypt=True;TrustServerCertificate=True" }
    'Sql'
    {
        if (-not $ConnectionString) { throw 'ConnectionString is required for Sql authentication.' }
        if ($PSCmdlet.ShouldProcess('connection string', 'encrypt with the key ring'))
        {
            $stored = (& $migrateExe --protect --key-ring $keyRing --connection-string $ConnectionString | Where-Object { $_ -match '^enc:v1:' } | Select-Object -Last 1)
            if (-not $stored) { throw 'wms-migrate --protect printed no enc:v1: string' }
        }
        else { $stored = 'enc:v1:(what-if)' }
    }
}

# 3. Services: registered with the account, configured through their environment, restarted on failure.
foreach ($service in $services)
{
    $folder = Join-Path $InstallRoot $service.Folder
    $binary = Join-Path $folder $service.Exe
    $account = Get-Account $service
    $environment = @(
        'Wms__Database__Provider=SqlServer',
        "Wms__Database__ConnectionString=$stored",
        "Wms__DataProtection__KeyRingPath=$keyRing",
        'Wms__Logging__EventLog__Source=Wolfgang.Wms',
        "Wms__Logging__File__Path=$logs\$($service.Folder)-.log",
        'Wms__Logging__Stdout=false'
    )
    if ($service.Url) { $environment += "ASPNETCORE_URLS=$($service.Url)" }

    if ($PSCmdlet.ShouldProcess($service.Name, "register as $account, binary $binary"))
    {
        $existing = Get-Service -Name $service.Name -ErrorAction SilentlyContinue
        if ($existing)
        {
            if ($existing.Status -ne 'Stopped') { Stop-Service -Name $service.Name -Force }
        }
        else
        {
            $parameters = @{ Name = $service.Name; BinaryPathName = "`"$binary`""; DisplayName = $service.Description; StartupType = 'Automatic'; Description = $service.Description }
            if ($Credential) { $parameters.Credential = $Credential }
            New-Service @parameters | Out-Null
            if (-not $Credential) { & sc.exe config $service.Name obj= $account | Out-Null }   # virtual account: no password, per-service identity
        }

        Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$($service.Name)" -Name Environment -Value ([string[]] $environment) -Type MultiString
        & sc.exe failure $service.Name reset= 86400 actions= restart/5000/restart/30000/restart/60000 | Out-Null
        foreach ($path in @($keyRing, $logs, $folder))
        {
            $acl = Get-Acl $path
            $rights = if ($path -eq $folder) { 'ReadAndExecute' } else { 'Modify' }
            $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($account, $rights, 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
            Set-Acl $path $acl
        }
    }
}

if ($PSCmdlet.ShouldProcess('Application log', 'register the Wolfgang.Wms event source') -and -not [System.Diagnostics.EventLog]::SourceExists('Wolfgang.Wms'))
{
    New-EventLog -LogName Application -Source 'Wolfgang.Wms'
}

# 4. Migrations with the caller's identity (the migration login, E15.4), then start and verify.
if ($PSCmdlet.ShouldProcess('wms', 'apply pending migrations'))
{
    $env:Wms__Database__Provider = 'SqlServer'
    $env:Wms__Database__ConnectionString = $stored
    $env:Wms__DataProtection__KeyRingPath = $keyRing
    & $migrateExe
    if ($LASTEXITCODE -ne 0) { throw "wms-migrate exited with $LASTEXITCODE" }
}

foreach ($service in $services)
{
    if ($PSCmdlet.ShouldProcess($service.Name, 'start'))
    {
        Start-Service -Name $service.Name
    }
}

if ($PSCmdlet.ShouldProcess($ApiUrl, 'verify /health/ready and a stop/start cycle'))
{
    $ready = $false
    for ($i = 0; $i -lt 30 -and -not $ready; $i++)
    {
        try { $ready = (Invoke-WebRequest -Uri "$ApiUrl/health/ready" -UseBasicParsing -SkipCertificateCheck -TimeoutSec 5).StatusCode -eq 200 } catch { Start-Sleep -Seconds 2 }
    }
    if (-not $ready) { throw "$ApiUrl/health/ready did not answer 200; see $logs\api-*.log and the Application event log." }
    Stop-Service -Name 'WolfgangWms.Api'
    Start-Service -Name 'WolfgangWms.Api'
    Write-Host "Install-WindowsServices: installed and verified. Console: $WebUrl  API: $ApiUrl  Logs: $logs"
}
