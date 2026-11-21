# Workflow Security

## Overview

This document describes the security model of the GitHub Actions workflows in this repository, centred on the PR validation workflow (`.github/workflows/pr.yaml`) and the protected-files guard (`.github/workflows/protected-files.yaml`) that keeps it honest.

The model in one paragraph: **PR checks run on `pull_request`, so the PR's own code and its own copies of the workflow and config files are what get built and tested, inside a run that GitHub gives a read-only token and no secrets when the PR comes from a fork or from Dependabot. Gate integrity comes from one small `pull_request_target` workflow that never checks out or executes anything from the PR: it lists the PR's changed files through the API and refuses any PR that changes a protected file alongside other changes.** A PR therefore cannot weaken a check it is subject to except as a configuration-only PR that a maintainer reviews on its own.

## Security Architecture

### 1. PR Checks Run in an Unprivileged Context

**Mechanism**: `pull_request` trigger (plus `push` to `main`)

```yaml
on:
  pull_request:
    branches:
      - main
  push:
    branches:
      - main
```

- The workflow file, the code and every analyzer/config file come from the PR's merge commit, not from `main`.
- GitHub gives a `pull_request` run from a fork (or by Dependabot) a **read-only `GITHUB_TOKEN`** and **no repository secrets**; `pr.yaml` declares `permissions: contents: read` as the default for every job, same-repo PRs included. Nothing in `pr.yaml` reads a secret.
- The push-to-`main` run re-runs the same gates on the merged result and keeps the InspectCode alert set on `main` current — a `pull_request` run attributes its SARIF to the PR ref, not to `main`.

**Why not `pull_request_target`?** `pr.yaml` used to run on it. Running the *base* branch's workflow against the *PR's* code gives that code the base repository's identity (secrets, a token that can be granted write scopes). Every job that built PR code was a place where a malicious MSBuild target or test could reach that identity, and Scorecard's *Dangerous-Workflow* and CodeQL's *untrusted-checkout* rightly flag the shape. Under `pull_request` a fork PR has nothing to reach.

### 2. Protected Files Guard

**Mechanism**: `protected-files.yaml` (job **`Protected Files Guard`**) — the one workflow on `pull_request_target`, and the reason it may be: it **never checks out or executes PR content**. It runs from `main` (a PR cannot edit it), lists the PR's changed files through the API (`pulls/{n}/files`), and classifies the PR:

| The PR changes… | Result |
|---|---|
| no protected file | pass — `pr.yaml` validated everything |
| **only** protected files | pass with a notice: this is the change that decides what future PRs are checked against, so a maintainer reviews the diff itself |
| protected files **and** anything else | **fail, and nothing can bypass it** — split the PR |

The guard fails closed: if the file list cannot be read, comes back empty, or is shorter than the PR's `changed_files` count (the endpoint caps at 3000 files), it refuses to classify and fails.

**Protected files** — everything that shapes what CI checks:
- `.github/workflows/*.yml` / `*.yaml` — the workflows themselves
- `.editorconfig` (root and nested), `*.globalconfig`, `*.ruleset`, `*.DotSettings` — analyzer and InspectCode severities
- `Directory.Build.props`, `Directory.Build.targets` (root and nested — MSBuild imports them by directory), `BannedSymbols.txt` — build properties and banned-API rules
- `coverlet.runsettings` — coverage collection (exclusions, instrumentation)
- `stryker-config.json` (root and nested) — the mutation-test floor `stryker.yaml` enforces
- `.config/dotnet-tools.json` — the pinned versions of the CI tools (`dotnet tool restore`); a PR must not be able to redirect which tool code CI executes
- `.github/license-audit/*.json` — license-audit policy, read by `license-audit.yaml`
- `.github/requirements/*` — the hash-pinned Python dependencies CI installs and then executes; the hashes live in the same file, so they are no defence against a PR editing it
- `.gitleaks.toml`, `.gitleaksignore` — secrets-scan rules, allowlist and ignored fingerprints
- `scripts/changelog.ps1`, `scripts/tfm-parity.ps1`, `scripts/build-pr.ps1`, `scripts/third-party-notices.ps1`, `scripts/Check-Migrations.ps1` — the CI scripts `pr.yaml` and `license-audit.yaml` run from the PR's tree (`Check-Migrations.ps1` is the EF migrations check Stage 1 runs)

Every name is matched case-insensitively (Windows and ReSharper resolve `foo.dotsettings` and `foo.DotSettings` to the same file, and a rename to `directory.build.props` is still the file MSBuild imports). Renames count for both the old and the new name; deletions count.

**Why the mixed-PR rule still exists.** Under `pull_request` a mixed PR *is* fully validated — the PR's config applies to the PR's code — so the old "validated against stale config" argument is gone. The rule stays for reviewability: a weakened `.editorconfig` hidden among forty code files is exactly the change that must not slip through, and forcing it into a PR of its own makes it the only thing the reviewer looks at.

`Protected Files Guard` is a **required status check** in this repository's `main` ruleset (confirmed 2026-11-16). `scripts/Setup-BranchRuleset.ps1` does not add it, so a re-created ruleset needs it added by hand. Dependabot PRs are exempt (the job is skipped for them): its bumps to `Directory.Build.props` are legitimate and its identity is GitHub-controlled.

**This repository is public, so it needs an Actions event policy for the guard.** GitHub's default Actions policy blocks `pull_request_target` in public repositories (enforced from 2026-12-02). A blocked run is a `startup_failure` with no jobs: the required check never reports, the PR is stuck, and the run cannot be re-run. Create a repository-level policy that allows the event for `.github/workflows/protected-files.yaml` only — never an unscoped one, which would block every other event. It is a repository setting and is created once.

### 3. Credential Protection

**Mechanism**: `persist-credentials: false`

Every checkout uses `persist-credentials: false`, so the job token is never written to `.git/config` where a build step could read it:

```yaml
- name: Checkout code
  uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1  # v7.0.1
  with:
    persist-credentials: false
```

**Note**: this keeps the token out of git config; it does not stop a step that is *explicitly* handed `${{ github.token }}` from using it. `pr.yaml` hands it to exactly one step — the `openapi-diff` job's "Post or update the PR comment", which reads only JSON from the PR and runs no PR code — and never stores it.

### 4. Minimal Permissions

```yaml
permissions:
  contents: read
```

is the workflow default. Two jobs declare more, scoped to the job:

- `openapi-diff` — `pull-requests: write`, to post its comment. It sparse-checks-out `docs/api` only and runs no PR code.
- `inspectcode` — `security-events: write` (+ `actions: read`), for the SARIF upload to Code Scanning.

### 5. Write Scopes and Fork / Dependabot PRs

**Mechanism**: the read-only token GitHub gives fork and Dependabot `pull_request` runs, plus a step-level condition on each writing step:

```yaml
if: >-
  github.event_name == 'push'
  || (github.event.pull_request.head.repo.full_name == github.repository && github.event.pull_request.user.login != 'dependabot[bot]')
```

A fork or Dependabot PR runs with a read-only token whatever the job declares, so its code can reach no write scope. The two writing steps — InspectCode's "Upload SARIF to Code Scanning" (above; it also runs on push to `main`) and `openapi-diff`'s comment step (same-repo, non-Dependabot PRs only) — are skipped for those PRs, because the API would refuse the read-only token. Fork and Dependabot PRs still get the InspectCode gate and the OpenAPI diff; only the upload and the comment are skipped. The author is tested, not `github.actor`: re-running a Dependabot PR makes the actor a maintainer while the token stays read-only.

For a **same-repo** PR, the `inspectcode` job checks out and builds the PR's code while holding `security-events: write`. The PR's author already has push access to this repository, so this is not a privilege escalation, but a PR's MSBuild targets, tests or analyzers could use that token to write Code Scanning results. Every other job that runs PR code — `detect-projects`, the two test stages (`test-linux-core`, `test-windows`), `aot-smoke`, `security-scan` — holds `contents: read` only.

### 6. Pinned Actions and Audited Workflow Files

Every external action referenced by `uses:` in `.github/workflows/` is pinned to a full commit SHA with a `# vMAJOR.MINOR.PATCH` comment (repository baseline item 11; a same-repository reference such as `uses: ./.github/workflows/docfx.yaml` needs no pin — it runs at the calling commit); Dependabot's `github-actions` ecosystem moves the pins. `actions-audit.yaml` runs on every PR: `actionlint` (workflow YAML + embedded shell via shellcheck) is a hard gate, and `zizmor` uploads every finding to the Security tab and **fails the job on High-severity findings**. A deliberate pattern zizmor objects to — `protected-files.yaml`'s `pull_request_target` is the standing example — is accepted with an inline `# zizmor: ignore[dangerous-triggers]` comment on the flagged key, never with a config file (`zizmor` only reads `.github/zizmor.yml`; a root `.zizmor.yml` is silently ignored). Because any such change touches a workflow file, it is a protected-file change and is reviewed by a maintainer.

## Attack Scenarios Prevented

### Scenario 1: Malicious Workflow Modification
**Attack**: PR modifies `.github/workflows/pr.yaml` to disable security checks
**Prevention**: The change is a protected file. Mixed with code, the guard fails and cannot be bypassed; on its own, it is a configuration-only PR a maintainer reviews line by line before it becomes the workflow future PRs run under. The guard itself runs from `main` and cannot be edited by the PR.
**Status**: ✅ Protected

### Scenario 2: Configuration File Tampering
**Attack**: PR modifies `.editorconfig` to disable security analyzers
**Prevention**: Same as Scenario 1 — protected file; mixed PRs fail, configuration-only PRs are reviewed in isolation
**Status**: ✅ Protected

### Scenario 3: Credential Theft
**Attack**: PR contains malicious code that tries to access GitHub credentials
**Prevention**: `pull_request` runs carry no secrets and a read-only token for fork and Dependabot PRs; `persist-credentials: false`; the token is handed to exactly one step, which runs no PR code
**Status**: ✅ Protected

### Scenario 4: Code Analysis Bypass
**Attack**: PR modifies `BannedSymbols.txt`, `.ruleset`, `.DotSettings` or `scripts/tfm-parity.ps1` to allow dangerous APIs or silence findings
**Prevention**: All are protected files (Scenario 1)
**Status**: ✅ Protected

### Scenario 5: Fork PR Code Reaches a Write Scope
**Attack**: A fork PR puts a payload in an MSBuild target, test or analyzer that forges or dismisses Code Scanning alerts with the job token
**Prevention**: A fork `pull_request` run gets a read-only token, and the SARIF upload is skipped for it (see §5)
**Status**: ✅ Protected (same-repo PRs: see the caveat in §5)

### Scenario 6: Workflow Regression to an Unpinned or Injectable Step
**Attack**: A PR re-introduces `uses: some/action@v1`, an over-broad `permissions:`, or `${{ }}` expanded into a `run:` script
**Prevention**: `actions-audit.yaml` fails on High-severity zizmor findings; the change is also a protected-file change (Scenario 1)
**Status**: ✅ Protected

### Scenario 7: Guard Evasion
**Attack**: A PR tries to disable the protected-files guard itself
**Prevention**: The guard runs from `main` (`pull_request_target`) regardless of what the PR's tree contains, and editing `protected-files.yaml` is itself a protected-file change
**Status**: ✅ Protected

## Validation

When reviewing changes to the workflow security model:

1. **Configuration-only PR**: open a PR that changes only `.editorconfig`; confirm `Protected Files Guard` passes with the notice and `pr.yaml` ran with the changed file.
2. **Mixed PR**: add a `.cs` change to that PR; confirm `Protected Files Guard` fails and the failure cannot be bypassed.
3. **Code-only PR**: confirm the guard passes silently and every gate ran on the merge commit.
4. **Fork PR**: confirm InspectCode runs and gates, and that its SARIF upload step and the `openapi-diff` comment step are skipped.
5. **Scanner view**: confirm that `protected-files.yaml` carries no checkout at all.

## Maintenance

### Making Changes to Protected Configuration Files

1. **Create a PR with your configuration changes — and nothing else.** `pr.yaml` runs with the changed configuration, so the run shows what it will do; `Protected Files Guard` passes with a notice listing the files. If the PR also changes anything else the guard fails: split the protected files into their own PR first.
2. **A maintainer reviews the diff itself and merges normally** — no bypass exists and the ruleset stays active. From then on every PR is checked against the new configuration.
3. **Relaxing a rule and adding code that needs it** therefore takes two PRs: the configuration change first (reviewed on its own), the code second. That is the point of the rule.

### Adding New Protected Files

1. Add the file to `protected-files.yaml` — the fixed-name `case` list for an exact (lowercased) path, the `*/name` branch for a name that can also appear nested, or the `grep -iE` pattern for a wildcard.
2. Update the list in this document and in [CONTRIBUTING.md](../CONTRIBUTING.md).

## References

- [GitHub Actions Security Hardening](https://docs.github.com/en/actions/security-guides/security-hardening-for-github-actions)
- [Keeping your GitHub Actions secure](https://docs.github.com/en/actions/security-guides/security-hardening-for-github-actions#using-third-party-actions)
- [Preventing pwn requests (why `pull_request_target` + PR checkout is dangerous)](https://securitylab.github.com/research/github-actions-preventing-pwn-requests/)
