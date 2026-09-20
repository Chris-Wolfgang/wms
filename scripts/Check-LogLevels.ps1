<#
.SYNOPSIS
    Checks the logging rules of docs/LOGGING.md (E12.2) across src/.

.DESCRIPTION
    Fails when a source file under src/ (the migrate and simulator command-line tools excepted) writes to the console, calls a logger
    with an interpolated string, or declares a [LoggerMessage] without a Level. Run it locally before a PR;
    the PR gate runs it too.
#>
[CmdletBinding()]
param
(
    [string] $Root = (Join-Path $PSScriptRoot '..')
)

$ErrorActionPreference = 'Stop'
$failures = New-Object System.Collections.Generic.List[string]
$files = Get-ChildItem -Path (Join-Path $Root 'src') -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin|Generated|Migrations)[\\/]' -and $_.FullName -notmatch '[\\/]Wolfgang\.Wms\.(Migrate|Simulator)[\\/]' }   # the two command-line tools write to the console by design

foreach ($file in $files)
{
    $lines = Get-Content -Path $file.FullName
    for ($i = 0; $i -lt $lines.Count; $i++)
    {
        $line = $lines[$i]
        $where = "$($file.FullName):$($i + 1)"
        if ($line -match '\bConsole\.(Write|WriteLine|Error\.Write)')
        {
            $failures.Add("$where`: Console output in a host or library; log through ILogger (docs/LOGGING.md).")
        }

        if ($line -match '\.Log(Trace|Debug|Information|Warning|Error|Critical)\s*\(\s*\$"')
        {
            $failures.Add("$where`: interpolated string in a log call; use a [LoggerMessage] method with a template.")
        }

        if ($line -match '\[LoggerMessage\(' -and $line -notmatch 'Level\s*=')
        {
            $failures.Add("$where`: [LoggerMessage] without an explicit Level.")
        }
    }
}

if ($failures.Count -gt 0)
{
    $failures | ForEach-Object { Write-Host "::error::$_" }
    Write-Host "Check-LogLevels: $($failures.Count) violation(s)."
    exit 1
}

Write-Host "Check-LogLevels: $($files.Count) files, no violations."
