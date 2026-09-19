<#
.SYNOPSIS
    Turns a scheduled workflow's outcome into exactly one actionable issue (E85.12).

.DESCRIPTION
    -Outcome failed: finds the open issue for this workflow + failure signature (marker
    "<!-- automated-failure: <workflow>|<signature> -->" in the body). If it exists, appends the run link as
    a comment; otherwise creates it with the failing output, the signature, a suggested fix and the run link,
    labelled `automated`, `nightly` and the concern labels given.

    -Outcome succeeded: closes every open issue carrying this workflow's marker with a comment naming the run.

    -Summary: upserts the weekly "automated issues open longer than N days" issue; issues labelled `critical`
    open longer than 2 days are listed first under an escalation heading and the summary title is prefixed
    "HIGH PRIORITY".

    Automation never edits workflow YAML or other protected paths: the issue is the trigger for a person or a
    session to diagnose and open a PR through pr.yaml.

.EXAMPLE
    pwsh scripts/report-failure.ps1 -Repository Chris-Wolfgang/wms -Workflow hygiene -Outcome failed `
        -Signature fragments -Title 'Changelog fragments older than 30 days' -BodyPath hygiene.md `
        -SuggestedFix 'Release, or delete fragments whose change was reverted.' -Labels process

.EXAMPLE
    pwsh scripts/report-failure.ps1 -Repository Chris-Wolfgang/wms -Workflow hygiene -Outcome succeeded

.EXAMPLE
    pwsh scripts/report-failure.ps1 -Repository Chris-Wolfgang/wms -Summary -OlderThanDays 7
#>
[CmdletBinding(DefaultParameterSetName = 'Outcome')]
param
(
    [Parameter(Mandatory)]
    [string]$Repository,

    [Parameter(Mandatory, ParameterSetName = 'Outcome')]
    [string]$Workflow,

    [Parameter(Mandatory, ParameterSetName = 'Outcome')]
    [ValidateSet('failed', 'succeeded')]
    [string]$Outcome,

    [Parameter(ParameterSetName = 'Outcome')]
    [string]$Signature = 'default',

    [Parameter(ParameterSetName = 'Outcome')]
    [string]$Title = '',

    [Parameter(ParameterSetName = 'Outcome')]
    [string]$BodyPath = '',

    [Parameter(ParameterSetName = 'Outcome')]
    [string]$SuggestedFix = '',

    [Parameter(ParameterSetName = 'Outcome')]
    [string[]]$Labels = @(),

    [Parameter(ParameterSetName = 'Summary', Mandatory)]
    [switch]$Summary,

    [Parameter(ParameterSetName = 'Summary')]
    [int]$OlderThanDays = 7,

    [string]$RunUrl = '',

    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:BaseLabels = @('automated', 'nightly')
$script:MaxBodyChars = 60000

if (-not $RunUrl -and $env:GITHUB_SERVER_URL -and $env:GITHUB_REPOSITORY -and $env:GITHUB_RUN_ID)
{
    $RunUrl = "$env:GITHUB_SERVER_URL/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"
}



function Invoke-Gh
{
    param([string[]]$Arguments)

    $out = & gh @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "gh $($Arguments -join ' ') failed: $out" }
    return $out
}



function Confirm-Label
{
    param([string]$Name, [string]$Color, [string]$Description)

    $existing = Invoke-Gh @('label', 'list', '-R', $Repository, '--search', $Name, '--json', 'name', '--jq', '.[].name')
    if (@($existing) -contains $Name) { return }
    if ($DryRun) { Write-Host "[dry-run] would create label '$Name'"; return }
    Invoke-Gh @('label', 'create', $Name, '-R', $Repository, '--color', $Color, '--description', $Description) | Out-Null
}



function Get-Marker
{
    param([string]$Sig)

    return "<!-- automated-failure: $Workflow|$Sig -->"
}



function Get-OpenAutomatedIssues
{
    $json = Invoke-Gh @('issue', 'list', '-R', $Repository, '--label', 'automated', '--state', 'open', '--limit', '200', '--json', 'number,title,body,createdAt,labels')
    return @(($json -join "`n") | ConvertFrom-Json)
}



function Get-BodyText
{
    if (-not $BodyPath) { return '' }
    if (-not (Test-Path $BodyPath)) { return "(no output file at $BodyPath)" }
    $text = Get-Content $BodyPath -Raw
    if ($text.Length -gt $script:MaxBodyChars)
    {
        $text = $text.Substring(0, $script:MaxBodyChars) + "`n… (truncated; full output in the run log)"
    }
    return $text
}



function Invoke-Failed
{
    $marker = Get-Marker $Signature
    $existing = @(Get-OpenAutomatedIssues | Where-Object { $_.body -and $_.body.Contains($marker) })
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm') + ' UTC'

    if ($existing.Count -gt 0)
    {
        $issue = $existing[0]
        $comment = "Failed again at $stamp`: $RunUrl`n`n🤖 Claude Code automation (`$Workflow`)"
        if ($DryRun) { Write-Host "[dry-run] would comment on #$($issue.number): $comment"; return }
        Invoke-Gh @('issue', 'comment', "$($issue.number)", '-R', $Repository, '--body', $comment) | Out-Null
        Write-Host "Appended run to #$($issue.number)"
        return
    }

    $issueTitle = if ($Title) { "[$Workflow] $Title" } else { "[$Workflow] Scheduled check failed ($Signature)" }
    $output = Get-BodyText
    $body = @(
        $marker
        "**Workflow:** ``$Workflow``  ·  **Signature:** ``$Signature``  ·  **First seen:** $stamp"
        "**Run:** $RunUrl"
        ''
        '## Failing output'
        ''
        $(if ($output) { $output } else { '_See the run log._' })
        ''
        '## Suggested fix'
        ''
        $(if ($SuggestedFix) { $SuggestedFix } else { 'Diagnose from the run log; fix the application code or content through a normal PR. Workflow YAML and other protected paths are never edited by automation.' })
        ''
        'This issue closes itself on the next successful run of the workflow; repeats append the run link as a comment.'
        ''
        "🤖 Claude Code automation (``$Workflow``)"
    ) -join "`n"

    $allLabels = @($script:BaseLabels + $Labels | Select-Object -Unique)
    Confirm-Label 'automated' 'bfd4f2' 'Opened by a scheduled workflow; closes itself on the next green run'
    Confirm-Label 'nightly' 'bfd4f2' 'Scheduled check'
    if ($DryRun)
    {
        Write-Host "[dry-run] would create '$issueTitle' with labels $($allLabels -join ', ')"
        Write-Host $body
        return
    }
    $args = @('issue', 'create', '-R', $Repository, '--title', $issueTitle, '--body', $body)
    foreach ($l in $allLabels) { $args += @('--label', $l) }
    $url = Invoke-Gh $args
    Write-Host "Created $url"
}



function Invoke-Succeeded
{
    $prefix = "<!-- automated-failure: $Workflow|"
    $open = @(Get-OpenAutomatedIssues | Where-Object { $_.body -and $_.body.Contains($prefix) })
    if ($open.Count -eq 0) { Write-Host "No open '$Workflow' issues to close"; return }
    foreach ($issue in $open)
    {
        $comment = "Resolved: the next run succeeded ($RunUrl).`n`n🤖 Claude Code automation (``$Workflow``)"
        if ($DryRun) { Write-Host "[dry-run] would close #$($issue.number)"; continue }
        Invoke-Gh @('issue', 'close', "$($issue.number)", '-R', $Repository, '--comment', $comment) | Out-Null
        Write-Host "Closed #$($issue.number)"
    }
}



function Invoke-Summary
{
    $marker = '<!-- automated-summary -->'
    $now = (Get-Date).ToUniversalTime()
    $all = @(Get-OpenAutomatedIssues | Where-Object { $_.body -and -not $_.body.Contains($marker) })
    $stale = @($all | Where-Object { ($now - [datetime]$_.createdAt).TotalDays -ge $OlderThanDays })
    $critical = @($all | Where-Object { ($_.labels.name -contains 'critical') -and ($now - [datetime]$_.createdAt).TotalDays -ge 2 })

    $lines = @($marker, "Automated issues open longer than $OlderThanDays days as of $($now.ToString('yyyy-MM-dd')).", '')
    if ($critical.Count -gt 0)
    {
        $lines += '## Escalation: critical, open longer than 2 days'
        $lines += ''
        foreach ($i in $critical) { $lines += "- #$($i.number) $($i.title) (opened $($i.createdAt.Substring(0, 10)))" }
        $lines += ''
    }
    $lines += "## Open longer than $OlderThanDays days"
    $lines += ''
    if ($stale.Count -eq 0) { $lines += '_None._' }
    foreach ($i in $stale) { $lines += "- #$($i.number) $($i.title) (opened $($i.createdAt.Substring(0, 10)))" }
    $lines += ''
    $lines += '🤖 Claude Code automation (`summary`)'
    $text = $lines -join "`n"
    $title = "$(if ($critical.Count -gt 0) { 'HIGH PRIORITY: ' })Automated issues open longer than $OlderThanDays days"

    $existing = @(Get-OpenAutomatedIssues | Where-Object { $_.body -and $_.body.Contains($marker) })
    if ($stale.Count -eq 0 -and $critical.Count -eq 0)
    {
        foreach ($i in $existing)
        {
            if ($DryRun) { Write-Host "[dry-run] would close summary #$($i.number)"; continue }
            Invoke-Gh @('issue', 'close', "$($i.number)", '-R', $Repository, '--comment', 'Nothing open longer than the threshold; closing the summary.') | Out-Null
        }
        Write-Host 'Nothing to summarise'
        return
    }
    if ($existing.Count -gt 0)
    {
        if ($DryRun) { Write-Host "[dry-run] would update summary #$($existing[0].number)`n$text"; return }
        Invoke-Gh @('issue', 'edit', "$($existing[0].number)", '-R', $Repository, '--title', $title, '--body', $text) | Out-Null
        Write-Host "Updated summary #$($existing[0].number)"
        return
    }
    Confirm-Label 'automated' 'bfd4f2' 'Opened by a scheduled workflow; closes itself on the next green run'
    if ($DryRun) { Write-Host "[dry-run] would create summary '$title'`n$text"; return }
    $url = Invoke-Gh @('issue', 'create', '-R', $Repository, '--title', $title, '--body', $text, '--label', 'automated')
    Write-Host "Created $url"
}



if ($Summary) { Invoke-Summary }
elseif ($Outcome -eq 'failed') { Invoke-Failed }
else { Invoke-Succeeded }
