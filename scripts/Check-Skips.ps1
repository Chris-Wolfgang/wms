<#
.SYNOPSIS
    Every skipped test must name an open issue (E13.1).

.DESCRIPTION
    Reads the TRX files under the given results directory; each result with outcome NotExecuted must carry a
    skip reason containing an issue reference (`#123` or `owner/repo#123`), and that issue must be open
    (checked with `gh issue view` unless -NoGitHub). Fails otherwise. A test that skips without an issue is
    a test silently switched off.
#>
[CmdletBinding()]
param
(
    [string] $ResultsDirectory = 'TestResults',
    [string] $Repository = 'Chris-Wolfgang/wms',
    [switch] $NoGitHub
)

$ErrorActionPreference = 'Stop'
$failures = New-Object System.Collections.Generic.List[string]
$skips = 0
$checked = @{}
$files = Get-ChildItem -Path $ResultsDirectory -Recurse -Filter *.trx -ErrorAction SilentlyContinue
if (-not $files)
{
    Write-Host "Check-Skips: no TRX files under $ResultsDirectory."
    exit 0
}

foreach ($file in $files)
{
    [xml] $trx = Get-Content -Path $file.FullName -Raw
    $ns = New-Object System.Xml.XmlNamespaceManager($trx.NameTable)
    $ns.AddNamespace('t', $trx.DocumentElement.NamespaceURI)
    foreach ($result in $trx.SelectNodes('//t:UnitTestResult[@outcome="NotExecuted"]', $ns))
    {
        $skips++
        $name = $result.GetAttribute('testName')
        $reason = ($result.SelectSingleNode('t:Output/t:ErrorInfo/t:Message', $ns)).InnerText
        if (-not $reason) { $reason = '' }
        $match = [regex]::Match($reason, '(?<repo>[\w.-]+/[\w.-]+)?#(?<number>\d+)')
        if (-not $match.Success)
        {
            $failures.Add("$name is skipped without an issue reference: '$reason'")
            continue
        }

        $repo = if ($match.Groups['repo'].Success) { $match.Groups['repo'].Value } else { $Repository }
        $key = "$repo#$($match.Groups['number'].Value)"
        if ($NoGitHub) { continue }
        if (-not $checked.ContainsKey($key))
        {
            $state = & gh issue view $match.Groups['number'].Value --repo $repo --json state --jq '.state' 2>$null
            $checked[$key] = $state
        }

        if ($checked[$key] -ne 'OPEN')
        {
            $failures.Add("$name is skipped for $key, which is not an open issue (state: '$($checked[$key])').")
        }
    }
}

if ($failures.Count -gt 0)
{
    $failures | ForEach-Object { Write-Host "::error::$_" }
    Write-Host "Check-Skips: $skips skipped test(s), $($failures.Count) without an open issue."
    exit 1
}

Write-Host "Check-Skips: $skips skipped test(s), every one names an open issue."
