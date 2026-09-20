<#
.SYNOPSIS
    Regenerates src/Wolfgang.Wms.Client/Generated from the committed OpenAPI document (E82.8, ADR 0004).

.DESCRIPTION
    Runs Kiota over docs/api/openapi-v<n>.json (default v0) into the client project's Generated folder with
    --clean-output, so the folder is exactly what the spec describes. Run it whenever the spec changes
    (OpenApiDocumentTests with WMS_UPDATE_OPENAPI=1 regenerates the spec first) and commit the result; the
    generated code carries [GeneratedCode("Kiota", ...)] and is excluded from coverage.

    Requires the Kiota tool: dotnet tool install --global Microsoft.OpenApi.Kiota

.EXAMPLE
    pwsh scripts/Update-ApiClient.ps1
#>
param
(
    [string]$Version = 'v0',

    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$spec = Join-Path $Root "docs/api/openapi-$Version.json"
$output = Join-Path $Root 'src/Wolfgang.Wms.Client/Generated'
if (-not (Test-Path $spec)) { throw "OpenAPI document not found: $spec" }

$kiota = Get-Command kiota -ErrorAction SilentlyContinue
if (-not $kiota)
{
    $fallback = Join-Path $HOME '.dotnet/tools/kiota'
    if ($IsWindows) { $fallback += '.exe' }
    if (-not (Test-Path $fallback)) { throw 'Kiota is not installed: dotnet tool install --global Microsoft.OpenApi.Kiota' }
    $kiota = Get-Command $fallback
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
