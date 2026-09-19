<#
.SYNOPSIS
    Repository hygiene checks for the weekly `hygiene.yaml` workflow (E85.11).

.DESCRIPTION
    Three checks, each a finding list; any finding fails the run so report-failure.ps1 opens the issue.

    fragments  Changelog fragments under changelog/unreleased added more than -FragmentMaxAgeDays ago (from
               `git log --diff-filter=A`). A fragment that old means a change has been sitting unreleased.
    skips      [Fact]/[Theory] with Skip = "..." whose reason does not name an issue (#N), or names a closed
               one. A skipped test without an open issue is a test that silently stopped existing.
    flags      Feature flags declared with a removal release (`new FeatureFlag("name", "vX.Y.Z")`) whose
               release is already tagged. A ship-dark flag past its removal release is dead branching.

    Writes a Markdown report to -OutFile (for the issue body) and GitHub annotations; exits 1 on findings.

.EXAMPLE
    pwsh scripts/hygiene.ps1 -Repository Chris-Wolfgang/wms -OutFile hygiene.md
#>
param
(
    [Parameter(Mandatory)]
    [string]$Repository,

    [string]$Root = '.',

    [int]$FragmentMaxAgeDays = 30,

    [string]$OutFile = '',

    [ValidateSet('fragments', 'skips', 'flags')]
    [string[]]$Checks = @('fragments', 'skips', 'flags'),

    [switch]$NoIssueLookup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path $Root).Path
$findings = [System.Collections.Generic.List[object]]::new()



function Add-Finding
{
    param([string]$Check, [string]$File, [string]$Message)

    $findings.Add([pscustomobject]@{ Check = $Check; File = $File; Message = $Message })
    Write-Host "::warning file=$File::[$Check] $Message"
}



function Test-Fragments
{
    $dir = Join-Path $Root 'changelog/unreleased'
    if (-not (Test-Path $dir)) { return }
    $now = (Get-Date).ToUniversalTime()
    foreach ($f in Get-ChildItem $dir -File -Filter *.md | Where-Object { $_.Name -ne 'README.md' })
    {
        $rel = "changelog/unreleased/$($f.Name)"
        $added = & git -C $Root log --diff-filter=A --format=%cI --follow -- $rel 2>$null | Select-Object -Last 1
        if (-not $added) { continue }
        $age = [int]($now - [datetime]$added).TotalDays
        if ($age -gt $FragmentMaxAgeDays)
        {
            Add-Finding 'fragments' $rel "added $age days ago (limit $FragmentMaxAgeDays); the change it describes is still unreleased"
        }
    }
}



function Test-Skips
{
    $tests = Join-Path $Root 'tests'
    if (-not (Test-Path $tests)) { return }
    $issueState = @{}
    foreach ($file in Get-ChildItem $tests -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
    {
        $rel = $file.FullName.Substring($Root.Length + 1) -replace '\\', '/'
        $lineNo = 0
        foreach ($line in Get-Content $file.FullName)
        {
            $lineNo++
            if ($line -notmatch 'Skip\s*=\s*"([^"]*)"') { continue }
            $reason = $Matches[1]
            if ($reason -notmatch '#(\d+)')
            {
                Add-Finding 'skips' "$rel`:$lineNo" "skipped test without an issue reference: `"$reason`" (add the tracking issue as #N)"
                continue
            }
            $n = $Matches[1]
            if ($NoIssueLookup) { continue }
            if (-not $issueState.ContainsKey($n))
            {
                $state = & gh issue view $n -R $Repository --json state --jq .state 2>$null
                $issueState[$n] = if ($LASTEXITCODE -eq 0) { $state } else { 'MISSING' }
            }
            if ($issueState[$n] -ne 'OPEN')
            {
                Add-Finding 'skips' "$rel`:$lineNo" "skipped test references #$n which is $($issueState[$n].ToLower()); un-skip the test or reopen the issue"
            }
        }
    }
}



function Get-LatestReleaseVersion
{
    $tags = @(& git -C $Root tag -l 'v*' 2>$null | Where-Object { $_ -match '^v\d+\.\d+\.\d+$' })
    if ($tags.Count -eq 0) { return $null }
    return ($tags | ForEach-Object { [version]($_.Substring(1)) } | Sort-Object | Select-Object -Last 1)
}



function Test-Flags
{
    $src = Join-Path $Root 'src'
    if (-not (Test-Path $src)) { return }
    $latest = Get-LatestReleaseVersion
    if (-not $latest) { return }
    foreach ($file in Get-ChildItem $src -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
    {
        $rel = $file.FullName.Substring($Root.Length + 1) -replace '\\', '/'
        $text = Get-Content $file.FullName -Raw
        foreach ($m in [regex]::Matches($text, 'new\s+FeatureFlag\s*\(\s*"([^"]+)"\s*,\s*"v?(\d+\.\d+\.\d+)"'))
        {
            $removal = [version]$m.Groups[2].Value
            if ($removal -le $latest)
            {
                Add-Finding 'flags' $rel "feature flag '$($m.Groups[1].Value)' was to be removed in $removal; $latest is released. Delete the flag and its dark branch."
            }
        }
    }
}



if ('fragments' -in $Checks) { Test-Fragments }
if ('skips' -in $Checks) { Test-Skips }
if ('flags' -in $Checks) { Test-Flags }

$report = [System.Text.StringBuilder]::new()
[void]$report.AppendLine("Hygiene run $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')) on ``$Repository``: $($findings.Count) finding(s).")
[void]$report.AppendLine()
foreach ($group in $findings | Group-Object Check)
{
    [void]$report.AppendLine("### $($group.Name)")
    [void]$report.AppendLine()
    foreach ($f in $group.Group) { [void]$report.AppendLine("- ``$($f.File)``: $($f.Message)") }
    [void]$report.AppendLine()
}
if ($OutFile) { Set-Content -Path $OutFile -Value $report.ToString() -NoNewline }
if ($env:GITHUB_STEP_SUMMARY) { Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $report.ToString() }

if ($findings.Count -gt 0)
{
    Write-Host "$($findings.Count) hygiene finding(s)"
    exit 1
}
Write-Host '✅ Hygiene checks passed'
