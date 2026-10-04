<#
.SYNOPSIS
    Regenerates src/Wolfgang.Wms.Client/Generated from the committed OpenAPI document (E82.8, ADR 0004).

.DESCRIPTION
    Runs Kiota over docs/api/openapi-v<n>.json (default v0) into the client project's Generated folder with
    --clean-output, so the folder is exactly what the spec describes. Run it whenever the spec changes
    (OpenApiDocumentTests with WMS_UPDATE_OPENAPI=1 regenerates the spec first) and commit the result; the
    generated code carries [GeneratedCode("Kiota", ...)] and is excluded from coverage.

    Requires the Kiota tool at the version recorded in Generated/kiota-lock.json (kiotaVersion), so a
    regeneration only changes the generated files when the spec changes:
    dotnet tool install --global Microsoft.OpenApi.Kiota --version <kiotaVersion>
    The script stops when the installed version differs from the lock.

.EXAMPLE
    pwsh scripts/Update-ApiClient.ps1
#>
param
(
    # One client, one Generated folder, one lock and namespace: only v0 exists. A second API version needs its own
    # output folder, lock and namespace (and WmsApiClient support) before it is accepted here.
    [ValidateSet('v0')]
    [string]$Version = 'v0',

    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$spec = Join-Path $Root "docs/api/openapi-$Version.json"
$output = Join-Path $Root 'src/Wolfgang.Wms.Client/Generated'
if (-not (Test-Path $spec)) { throw "OpenAPI document not found: $spec" }

$lock = Join-Path $output 'kiota-lock.json'
$pinned = (Get-Content $lock -Raw | ConvertFrom-Json).kiotaVersion
$install = "dotnet tool install --global Microsoft.OpenApi.Kiota --version $pinned"

$kiota = Get-Command kiota -ErrorAction SilentlyContinue
if (-not $kiota)
{
    $fallback = Join-Path $HOME '.dotnet/tools/kiota'
    if ($IsWindows) { $fallback += '.exe' }
    if (-not (Test-Path $fallback)) { throw "Kiota is not installed: $install" }
    $kiota = Get-Command $fallback
}

# kiota --version prints e.g. "1.35.0+114aa7ee..."; compare the release part with the lock.
$installed = ((& $kiota.Source --version) | Select-Object -First 1).Split('+')[0].Trim()
if ($installed -ne $pinned)
{
    throw "Kiota $installed is installed but the client was generated with $pinned (kiota-lock.json). Install the pinned version: dotnet tool update --global Microsoft.OpenApi.Kiota --version $pinned"
}

& $kiota.Source generate `
    --language CSharp `
    --openapi $spec `
    --class-name WmsClient `
    --namespace-name Wolfgang.Wms.Client.Generated `
    --output $output `
    --clean-output `
    --exclude-backward-compatible `
    --log-level Warning
if ($LASTEXITCODE -ne 0) { throw "kiota generate failed with exit code $LASTEXITCODE" }

Remove-Item (Join-Path $output '.kiota.log') -ErrorAction SilentlyContinue
Write-Host "Regenerated $output from $spec"
