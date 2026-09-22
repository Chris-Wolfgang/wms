#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Creates (or updates) one GitHub issue per epic and per story from a plan chapter, with sub-issue links.
.DESCRIPTION
    Parses a chapter written as:

        ## E<n> Title                      -> epic
        As a ..., I want ... so ...        -> story sentence (first non-empty line after the heading)
        ### E<n>.<m> Title                 -> story
        As a ..., I want ...
        - AC: ...                          -> acceptance criteria (kept verbatim)

    and upserts issues titled "<ID> <Title>" (the ID is the stable key; the GitHub number is incidental):

      - epic body  : sentence, any extra prose, then a task list of its stories (written after they exist)
      - story body : sentence, then "### Acceptance criteria" with the AC lines verbatim
      - labels     : epic|story, the phase label, the epic's area label, plus paid-feature where the AC
                     say a feature is paid
      - milestone  : created if missing (no due date)
      - sub-issues : every story attached to its epic via the sub-issues API

    Idempotent: an existing issue with the same exact title is updated, never duplicated; sub-issue links
    already present are skipped. Content-creating calls are spaced out to stay under GitHub's secondary
    rate limit (~80 content requests per minute).
.PARAMETER ChapterPath
    The Markdown chapter to parse.
.PARAMETER Repository
    owner/name. Defaults to the current repository.
.PARAMETER Phase
    Label applied to every issue (default phase-1).
.PARAMETER Milestone
    Milestone title (default v0.1.0).
.PARAMETER AreaMap
    Hashtable epic-ID -> area label. Epics not in the map get no area label (reported).
.PARAMETER DryRun
    Parse and print the plan; change nothing.
.EXAMPLE
    pwsh ./scripts/Create-PlanIssues.ps1 -ChapterPath ../wms-plan/01-phase-1-foundation.md -DryRun
#>

[CmdletBinding()]
param
(
    [Parameter(Mandatory)]
    [string]$ChapterPath,
    [string]$Repository,
    [string]$Phase = 'phase-1',
    [string]$Milestone = 'v0.1.0',
    [hashtable]$AreaMap = @{
        E86 = 'repo'; E1 = 'repo'; E85 = 'repo'
        E82 = 'api'
        E2 = 'database'; E3 = 'database'; E4 = 'database'; E5 = 'database'
        E6 = 'settings'; E7 = 'settings'
        E8 = 'security'; E9 = 'security'; E10 = 'security'; E11 = 'security'
        E12 = 'ops'; E13 = 'ops'; E14 = 'ops'; E15 = 'ops'
        E79 = 'licensing'
        E83 = 'docs'
    },
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Repository) { $Repository = (& gh repo view --json nameWithOwner --jq .nameWithOwner) }
$labelColors = @{
    epic = '5319e7'; story = '1d76db'; 'paid-feature' = 'fbca04'
    repo = 'c5def5'; api = '0e8a16'; database = 'bfd4f2'; settings = 'd4c5f9'; security = 'd93f0b'
    ops = 'f9d0c4'; licensing = 'e99695'; docs = '0075ca'
}
$labelColors[$Phase] = 'ededed'
$paidPattern = '(?i)\bpaid feature\b|\bis paid\b'     # the story's own feature is paid, not licensing mechanics
$writeDelayMs = 900     # ~65 content requests/minute



function Invoke-Api
{
    param([string]$Method, [string]$Path, [hashtable]$Body, [switch]$Paginate)

    $args = @('api', '-X', $Method, '-H', 'Accept: application/vnd.github+json', '-H', 'X-GitHub-Api-Version: 2022-11-28')
    if ($Paginate) { $args += @('--paginate', '--slurp') }
    if ($Body) { $args += @('--input', '-') }
    $args += $Path
    $json = if ($Body) { $Body | ConvertTo-Json -Depth 10 -Compress } else { $null }
    $raw = if ($json) { $json | & gh @args 2>&1 } else { & gh @args 2>&1 }
    $out = ($raw | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] } | Out-String)
    $err = ($raw | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } | ForEach-Object { $_.ToString() } | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "gh api $Method $Path failed: $err $out" }
    if ($Method -ne 'GET') { Start-Sleep -Milliseconds $writeDelayMs }
    if (-not $out.Trim()) { return $null }
    $data = $out | ConvertFrom-Json -Depth 20
    if ($Paginate) { $data = @($data | ForEach-Object { @($_) }) }
    return $data
}



function Read-Chapter
{
    param([string]$Path)

    $epics = [System.Collections.Generic.List[object]]::new()
    $current = $null
    $problems = @()
    $lineNo = 0
    foreach ($line in Get-Content $Path -Encoding utf8)
    {
        $lineNo++
        if ($line -match '^## (E\d+) (.+)$')
        {
            $current = [pscustomobject]@{ Id = $Matches[1]; Title = $Matches[2].Trim(); Sentence = $null; Extra = @(); Stories = [System.Collections.Generic.List[object]]::new(); Number = $null; NodeId = $null }
            $epics.Add($current); $target = $current; continue
        }
        if ($line -match '^### (E\d+\.\d+) (.+)$')
        {
            if (-not $current) { $problems += "line ${lineNo}: story before any epic"; continue }
            $story = [pscustomobject]@{ Id = $Matches[1]; Title = $Matches[2].Trim(); Sentence = $null; Ac = @(); Extra = @(); Epic = $current; Number = $null; NodeId = $null; Paid = $false }
            if (-not $story.Id.StartsWith($current.Id + '.')) { $problems += "line ${lineNo}: $($story.Id) is under epic $($current.Id)" }
            $current.Stories.Add($story); $target = $story; continue
        }
        if ($line -match '^#')
        {
            if ($line -match '^# ') { continue }                    # chapter title
            $problems += "line ${lineNo}: unclassified heading: $line"; continue
        }
        if (-not $line.Trim()) { continue }
        if ($null -eq $current) { continue }                       # chapter intro
        if (-not $target.Sentence) { $target.Sentence = $line.Trim(); continue }
        if ($target.PSObject.Properties['Ac'] -and $line -match '^- AC:') { $target.Ac += $line.Trim(); continue }
        $target.Extra += $line.TrimEnd()
    }
    foreach ($e in $epics) { foreach ($s in $e.Stories) { $s.Paid = [bool](($s.Ac -join ' ') -match $paidPattern) } }
    return @{ Epics = $epics; Problems = $problems }
}



function Get-StoryBody
{
    param($Story)

    $lines = @($Story.Sentence)
    if ($Story.Extra.Count -gt 0) { $lines += ''; $lines += $Story.Extra }
    $lines += ''; $lines += '### Acceptance criteria'; $lines += ''
    $lines += ($Story.Ac | ForEach-Object { $_ -replace '^- AC:\s*', '- ' })
    return ($lines -join "`n")
}



function Get-EpicBody
{
    param($Epic)

    $lines = @($Epic.Sentence)
    if ($Epic.Extra.Count -gt 0) { $lines += ''; $lines += $Epic.Extra }
    $lines += ''; $lines += '### Stories'; $lines += ''
    foreach ($s in $Epic.Stories)
    {
        $ref = if ($s.Number) { " (#$($s.Number))" } else { '' }
        $lines += "- [ ] $($s.Id) $($s.Title)$ref"
    }
    return ($lines -join "`n")
}



function Get-Labels
{
    param($Item, [bool]$IsEpic)

    $epicId = if ($IsEpic) { $Item.Id } else { $Item.Epic.Id }
    $labels = @($(if ($IsEpic) { 'epic' } else { 'story' }), $Phase)
    if ($AreaMap.ContainsKey($epicId)) { $labels += $AreaMap[$epicId] }
    if (-not $IsEpic -and $Item.Paid) { $labels += 'paid-feature' }
    return $labels
}

# ---------------------------------------------------------------------------
$parsed = Read-Chapter $ChapterPath
$epics = $parsed.Epics
$storyCount = ($epics | ForEach-Object { $_.Stories.Count } | Measure-Object -Sum).Sum
Write-Host "Parsed $($epics.Count) epic(s), $storyCount story(ies) from $ChapterPath"
if ($parsed.Problems.Count -gt 0) { $parsed.Problems | ForEach-Object { Write-Warning $_ } }
$unmapped = @($epics | Where-Object { -not $AreaMap.ContainsKey($_.Id) } | ForEach-Object { $_.Id })
if ($unmapped.Count -gt 0) { Write-Warning "epics without an area label: $($unmapped -join ', ')" }

if ($DryRun)
{
    foreach ($e in $epics)
    {
        Write-Host ("{0,-8} {1,-55} [{2}]" -f $e.Id, $e.Title, ((Get-Labels $e $true) -join ', '))
        foreach ($s in $e.Stories) { Write-Host ("  {0,-8} {1,-53} AC={2,2} [{3}]" -f $s.Id, $s.Title, $s.Ac.Count, ((Get-Labels $s $false) -join ', ')) }
    }
    Write-Host ''; Write-Host "dry run: nothing created. paid-feature stories: $(($epics | ForEach-Object { $_.Stories } | Where-Object { $_.Paid } | ForEach-Object { $_.Id }) -join ', ')"
    exit 0
}

# Labels
$existingLabels = @(& gh label list -R $Repository --limit 300 --json name --jq '.[].name')
foreach ($name in $labelColors.Keys)
{
    if ($name -notin $existingLabels)
    {
        & gh label create $name -R $Repository --color $labelColors[$name] --description "Plan: $name" | Out-Null
        Write-Host "created label $name"; Start-Sleep -Milliseconds $writeDelayMs
    }
}

# Milestone
$milestones = @(Invoke-Api GET "repos/$Repository/milestones?state=all&per_page=100")
$ms = $milestones | Where-Object { $_.title -eq $Milestone } | Select-Object -First 1
if (-not $ms) { $ms = Invoke-Api POST "repos/$Repository/milestones" @{ title = $Milestone }; Write-Host "created milestone $Milestone (#$($ms.number))" }
$milestoneNumber = $ms.number

# Existing issues by exact title (issues endpoint also returns PRs; skip those)
$all = @(Invoke-Api GET "repos/$Repository/issues?state=all&per_page=100" -Paginate) | Where-Object { -not $_.PSObject.Properties['pull_request'] }
$byTitle = @{}
foreach ($i in $all) { if (-not $byTitle.ContainsKey($i.title)) { $byTitle[$i.title] = $i } }



function Set-Issue
{
    param($Item, [bool]$IsEpic, [string]$Body)

    $title = "$($Item.Id) $($Item.Title)"
    $payload = @{ title = $title; body = $Body; labels = @(Get-Labels $Item $IsEpic); milestone = $milestoneNumber }
    if ($byTitle.ContainsKey($title))
    {
        $existing = $byTitle[$title]
        $r = Invoke-Api PATCH "repos/$Repository/issues/$($existing.number)" $payload
        Write-Host ("  updated #{0,-4} {1}" -f $r.number, $title)
    }
    else
    {
        $r = Invoke-Api POST "repos/$Repository/issues" $payload
        $byTitle[$title] = $r
        Write-Host ("  created #{0,-4} {1}" -f $r.number, $title)
    }
    $Item.Number = $r.number; $Item.NodeId = $r.id
}

Write-Host ''; Write-Host '== epics'
foreach ($e in $epics) { Set-Issue $e $true (Get-EpicBody $e) }

Write-Host ''; Write-Host '== stories'
foreach ($e in $epics) { foreach ($s in $e.Stories) { Set-Issue $s $false (Get-StoryBody $s) } }

Write-Host ''; Write-Host '== sub-issue links'
foreach ($e in $epics)
{
    $linked = @(Invoke-Api GET "repos/$Repository/issues/$($e.Number)/sub_issues?per_page=100" -Paginate | ForEach-Object { $_.number })
    $added = 0
    foreach ($s in $e.Stories)
    {
        if ($s.Number -in $linked) { continue }
        Invoke-Api POST "repos/$Repository/issues/$($e.Number)/sub_issues" @{ sub_issue_id = $s.NodeId } | Out-Null
        $added++
    }
    Write-Host ("  {0,-5} #{1,-4} {2} linked, {3} added" -f $e.Id, $e.Number, $linked.Count, $added)
}

Write-Host ''; Write-Host '== epic checklists (with issue numbers)'
foreach ($e in $epics) { Set-Issue $e $true (Get-EpicBody $e) | Out-Null }

Write-Host ''; Write-Host '== verification'
$ok = $true
foreach ($e in $epics)
{
    $count = @(Invoke-Api GET "repos/$Repository/issues/$($e.Number)/sub_issues?per_page=100" -Paginate).Count
    $flag = if ($count -eq $e.Stories.Count) { 'ok ' } else { $ok = $false; 'BAD' }
    Write-Host ("  {0} {1,-5} #{2,-4} sub-issues {3}/{4}" -f $flag, $e.Id, $e.Number, $count, $e.Stories.Count)
}

Write-Host ''; Write-Host '| ID | Issue | Title |'; Write-Host '|---|---|---|'
foreach ($e in $epics)
{
    Write-Host "| $($e.Id) | [#$($e.Number)](https://github.com/$Repository/issues/$($e.Number)) | $($e.Title) |"
    foreach ($s in $e.Stories) { Write-Host "| $($s.Id) | [#$($s.Number)](https://github.com/$Repository/issues/$($s.Number)) | $($s.Title) |" }
}
if (-not $ok) { Write-Error 'one or more epics have a sub-issue count mismatch'; exit 1 }
