# Copilot Coding Agent Instructions

## Repository Summary

Wolfgang.Wms is a warehouse management system built as a modular monolith: one API, one database, one
deployable, with the picking module first. It is an application that is installed (a database, the API, the
Blazor Server console, a worker, and an Android handheld), not a library; the only NuGet package it publishes
is `Wolfgang.Wms.Client`, the generated API client. The product documentation for operators is under
`docfx_project/docs/`; the engineering documentation is under `docs/` (start with
[docs/DELIVERY-PROCESS.md](../docs/DELIVERY-PROCESS.md), the map of how work reaches `main` and a release).

**Repository Type**: Application (a solution, `Wolfgang.Wms.slnx`, with 18 projects under `src/`, 3 under `tests/`, 1 under `benchmarks/`)
**Target Platform**: .NET 10 only (`net10.0` everywhere; the handheld is `net10.0-android`). There is no multi-TFM matrix.
**Databases**: SQL Server 2022+ (Express included) and PostgreSQL 16+, from one EF Core model with per-provider migrations.
**Primary Language**: C#; the `scripts/` tooling is PowerShell 7 (`pwsh`). The one shell script is the git hook `.githooks/pre-commit` (bash, as git hooks must be); CI `run:` steps use bash on Linux and pwsh on Windows.

## Build and Validation Instructions

### Prerequisites
- .NET 10 SDK. The Android project needs the `maui-android` workload (`dotnet workload restore`); everything else builds without it.
- PowerShell 7 (`pwsh`) for every script under `scripts/`.
- For the SQL Server integration tests: Docker (Testcontainers), or a local SQL Server / Express LocalDB named by `WMS_TEST_SQLSERVER`; the PostgreSQL tests need Docker. Without either, those tests skip and say so.
- A bash on PATH for the pre-commit hook (`.githooks/pre-commit`; Git for Windows ships one) plus the optional gitleaks CLI (`git config core.hooksPath .githooks`).

### Build Process

1. **Restore and build** (warnings are errors in every configuration, E1.2):
   ```powershell
   dotnet restore
   dotnet build --no-restore --configuration Release
   ```

2. **Run the PR workflow's Windows stage locally** — build, tests, coverage gates, DevSkim, gitleaks:
   ```powershell
   pwsh ./scripts/build-pr.ps1
   ```
   `-SkipSecurity` / `-SkipCoverage` / `-SkipTests` narrow it while iterating. The Linux stage, InspectCode, the AOT smoke and the OpenAPI diff only run in CI.

3. **Format**: `pwsh ./scripts/format.ps1` (`-Check` to verify only). The PR workflow does not run a format check; formatting is developer-side.

4. **Schema**: a model change needs a migration for both providers; `pwsh ./scripts/Check-Migrations.ps1` is what CI runs (see [docs/MIGRATE.md](../docs/MIGRATE.md) and [docs/DATABASE-CONVENTIONS.md](../docs/DATABASE-CONVENTIONS.md)). The API never migrates; `wms-migrate` does.

### Critical Build Requirements
- **Test projects**: `tests/Wolfgang.Wms.UnitTests`, `tests/Wolfgang.Wms.IntegrationTests` (xunit; a framework on which zero tests ran fails the stage) and `tests/Wolfgang.Wms.AotSmoke` (`IsTestProject=false`; published NativeAOT and run by the AOT smoke job).
- **Code coverage**: **90 %** line coverage per assembly under `src/` (`Wolfgang.Wms.Domain` 100 %), **100 %** per test assembly, measured on the Linux stage; the Windows stage reports test assemblies only, because Docker-only tests skip there.
- **Changelog fragment**: a PR that changes anything under `src/` must add `changelog/unreleased/<name>.md` (`type: breaking|feature|fix|docs|internal` + one user-facing sentence) or carry the `no-changelog` label; a `feature` or `breaking` fragment must come with a documentation change or the `no-docs` label (E85.10). `CHANGELOG.md` is never edited by hand. See `changelog/unreleased/README.md`.
- **Protected configuration files**: `.editorconfig`, `Directory.Build.props`, `Directory.Build.targets`, `BannedSymbols.txt`, `coverlet.runsettings`, `*.globalconfig`, `*.ruleset`, `*.DotSettings` and anything under `.github/workflows/` are re-fetched from `main` during CI, and a PR that changes them **fails the `Detect .NET Projects` check by design** so a maintainer reviews the diff and merges with the admin bypass. Keep such changes in their own PR. See `docs/WORKFLOW_SECURITY.md`.
- **Security scanning**: gitleaks, DevSkim (any finding fails), CodeQL (security-extended), Semgrep, ReSharper InspectCode (error-severity findings fail), actionlint + zizmor on the workflow files (High-severity zizmor findings fail).
- **Documentation**: `GenerateDocumentationFile` is on for every project under `src/` except the two provider migrations projects (`Wolfgang.Wms.Infrastructure.Migrations.*`, generated code, set to `false`); elsewhere a public member without an XML doc comment is CS1591 → build error.
- **Banned APIs**: `BannedSymbols.txt` rejects blocking waits, sync I/O, `Parallel.*` and obsolete APIs at compile time; all I/O is async.
- **AOT**: every non-UI product project has `IsAotCompatible`/`IsTrimmable` on and the trim/AOT analyzers fail the build; `Wolfgang.Wms.Infrastructure` (EF Core) is the documented exception. See [docs/CODING-CONVENTIONS.md](../docs/CODING-CONVENTIONS.md).

### Common Issues and Workarounds
- **Coverage threshold failures**: the build fails below the gate by design; the gate reads ReportGenerator's `Summary.txt` per assembly.
- **`Detect .NET Projects` red with "PROTECTED CONFIGURATION FILES CHANGED"**: expected for any workflow/config change — it is the review signal, not a bug to fix.
- **`Changelog Fragment Check` red**: add the fragment (and the docs change, or the `no-docs` label), or the `no-changelog` label, then re-run the job.
- **SQL Server tests skipped locally**: set `WMS_TEST_SQLSERVER` (Express LocalDB works) or start Docker.
- **Stacked PRs**: a PR whose base is not `main` gets only the ancillary checks; the full gate runs when it is retargeted to `main`. See `docs/STACKED-PRS.md` and `scripts/restack.ps1`.

## Project Layout and Architecture

```
root/
├── Wolfgang.Wms.slnx           # The solution
├── src/
│   ├── Wolfgang.Wms.Domain     # Entities, value objects, invariants (100 % coverage)
│   ├── Wolfgang.Wms.Core       # Application layer: modules, endpoints, caching, paging, ETags, problem details
│   ├── Wolfgang.Wms.Infrastructure                      # EF Core model, conventions, repositories, migrations runner
│   ├── Wolfgang.Wms.Infrastructure.Migrations.SqlServer # Per-provider migrations
│   ├── Wolfgang.Wms.Infrastructure.Migrations.PostgreSql
│   ├── Wolfgang.Wms.Api        # The ASP.NET Core host (/api/v0, OpenAPI document committed under docs/api)
│   ├── Wolfgang.Wms.Web        # Blazor Server console; Web.Shared + one project per workspace
│   ├── Wolfgang.Wms.Web.{Configure,Supervise,Resolve,Report,Insights}
│   ├── Wolfgang.Wms.Worker     # Background host
│   ├── Wolfgang.Wms.Migrate    # wms-migrate: the only thing that changes the schema
│   ├── Wolfgang.Wms.Simulator  # Load/scenario simulator (NativeAOT)
│   ├── Wolfgang.Wms.Client     # Generated Kiota API client (the NuGet package)
│   └── Wolfgang.Wms.Android    # .NET MAUI handheld
├── tests/                      # UnitTests, IntegrationTests (Testcontainers / LocalDB), AotSmoke
├── benchmarks/Wolfgang.Wms.Benchmarks
├── changelog/unreleased/       # One changelog fragment per src/-touching PR
├── docs/                       # Engineering docs: conventions, delivery process, ADRs, API, migrations
├── docfx_project/              # Operator docs and the API reference site (built to gh-pages)
├── scripts/                    # PowerShell tooling (build-pr, changelog, format, Check-Migrations, Update-ApiClient, rulesets, restack)
├── .claude/skills/             # pr-gate, prerelease-review, release, hotfix: the author-side checklists per gate
├── .githooks/pre-commit        # gitleaks secret scan
└── .github/                    # Workflows, issue/PR templates, CODEOWNERS, dependabot, license-audit, pip requirements
```

### Key Configuration Files
- **`.editorconfig`**: Code style rules and analyzer severities, layered per directory (`src/` strictest; `tests/`, `benchmarks/` relaxed). File-scoped namespaces, `var` where the type is apparent, Allman braces.
- **`Directory.Build.props`**: Shared MSBuild properties: the analyzer packages, `TreatWarningsAsErrors` in every configuration, `GenerateDocumentationFile` for `src/`, the AOT/trim analyzers, `IsPackable` off by default, MinVer versioning from tags, SourceLink.
- **`BannedSymbols.txt`**: Async-first enforcement (blocking waits, sync I/O, `Parallel.*`, obsolete APIs).
- **`coverlet.runsettings`**: coverage collection; test assemblies instrumented too; migrations assemblies and generated code excluded.
- **`.gitleaks.toml`**: gitleaks allowlist (extends the default rules).
- **`docs/api/openapi-v0.json`**: the committed OpenAPI document; `OpenApiDocumentTests` keeps it equal to what the host serves and CI diffs it against `main`.
- **`CONTRIBUTING.md`**, **`SECURITY.md`**, **`CODE_OF_CONDUCT.md`**: contribution guidelines, reporting channel + response timelines, Contributor Covenant.

### Conventions worth knowing before editing
- [docs/CODING-CONVENTIONS.md](../docs/CODING-CONVENTIONS.md): layering, async-only, AOT, publish shape, test naming.
- [docs/API-CONVENTIONS.md](../docs/API-CONVENTIONS.md) and [docs/API-VERSIONING.md](../docs/API-VERSIONING.md): `/api/v0`, keyset paging, ETags and `If-Match`, idempotency keys, problem details with stable error codes.
- [docs/DATABASE-CONVENTIONS.md](../docs/DATABASE-CONVENTIONS.md): naming on both engines, `row_version` on versioned tables, the conventions test that proves the two schemas match.
- [docs/adr/](../docs/adr/): the architecture decision records (read models and caching by row version, data conventions).

### GitHub Integration
- **Workflows** (`.github/workflows/`, 16): `pr.yaml` (the gated PR pipeline), `release.yaml` (published release → packages, attestation, docs), `docfx.yaml` (versioned docs deploy, called by release), `build-all-versions.yaml`, `codeql.yaml`, `actions-audit.yaml` (actionlint + zizmor), `semgrep.yaml`, `license-audit.yaml`, `sbom.yaml`, `sourcelink.yaml`, `scorecard.yaml`, `security-alerts.yml` (nightly alert → issue triage), `docs-check.yaml` (nightly link check), `hygiene.yaml`, `stryker.yaml`, `benchmarks.yaml`.
- **Issue templates** (YAML forms): bug report, feature request, maintenance task.
- **PR template**: structured checklist; the `pr-gate` skill is the author-side version.
- **CODEOWNERS**: default owner `@Chris-Wolfgang`.
- **Dependabot**: NuGet, GitHub Actions and the hash-pinned pip tooling under `.github/requirements/`, weekly, grouped, labelled `dependencies`.

### Continuous Integration Pipeline (`.github/workflows/pr.yaml`)
Runs on `pull_request_target` (the workflow file and protected config come from `main`; the PR head is checked out for the code):

1. **Secrets Scan (gitleaks)** and **Detect .NET Projects** (project discovery + the protected-file guard) run first.
2. **Changelog Fragment Check**, **ReSharper InspectCode**, **OpenAPI Diff (v0)** and the **AOT Smoke** run in parallel with the test stages.
3. **Stage 1: Linux Tests (.NET 5.0-10.0) + Coverage Gate** (Docker-backed integration tests; every coverage gate) → **Stage 2: Windows Tests** (Express LocalDB for the SQL Server tests; src gates, test assemblies reported). There is no macOS stage.
4. **Security Scan (DevSkim)**; CodeQL, actionlint/zizmor, Semgrep, license audit, SBOM and SourceLink run from their own workflows.
5. **Required checks** (ruleset, `scripts/Setup-BranchRuleset.ps1`): every gate that is written to fail the PR blocks the merge: Detect .NET Projects, Stage 1, Stage 2, ReSharper InspectCode, AOT Smoke, OpenAPI Diff, DevSkim, gitleaks, Changelog Fragment Check, CodeQL, actionlint, zizmor, Semgrep, License audit, Generate SBOM, Verify SourceLink.

### Branch Protection Configuration
The ruleset is defined by `scripts/Setup-BranchRuleset.ps1` (single-developer defaults: no approvals, required checks, conversation resolution, no force pushes, CodeQL / Copilot review / code-quality rules, optional `-RequireLinearHistory`) and recreated by `scripts/Fix-BranchRuleset.ps1` when the definition changes. Both auto-detect the repository from `gh repo view`; both need an admin `gh auth login`.

## Delivery process

[docs/DELIVERY-PROCESS.md](../docs/DELIVERY-PROCESS.md) is the map: the PR gate (E85.1, `pr-gate` skill), the main gate (arrives with E83), the prerelease gate (`prerelease-review` skill, `vX.Y.Z-rc.N`), the release gate (`release` skill, `vX.Y.Z`, `release.yaml`), and hotfixes (`hotfix` skill, `release/N.x`). Versions come from git tags via MinVer; nothing in a csproj carries a version. Work is tracked as epics and stories (`E<epic>.<story>`), and a PR title carries its story ID.

## Maintenance Framework

Ongoing improvement work (security, performance, testing, cleanup, docs, API, CI/CD) is tracked with the fleet-wide **Maintenance** framework: one parent `Maintenance: wms` issue (label `maintenance`, never closed), sub-issues labelled `maintenance-task` plus one `maintenance - <category>` label, aggregated on a cross-repo GitHub Projects board.

| Label | Covers |
|---|---|
| `maintenance - security` | SAST/analyzer scans, finding fixes, dependency vulnerability audit |
| `maintenance - performance` | Profile, benchmark, optimize, validate gains |
| `maintenance - testing` | Coverage %, integration/smoke/mutation tests, fixtures, CI test-step additions |
| `maintenance - cleanup` | Refactor for reuse, quality, efficiency |
| `maintenance - docs` | XML doc coverage, README, CHANGELOG, samples |
| `maintenance - API` | Public/internal surface audit, breaking-change vigilance |
| `maintenance - CI/CD` | Docker, CI workflow, build/publish pipeline, packaging |

### For Copilot agents working in this repo

When you **discover work** that fits one of the categories — a security scanner finding, a slow hot path, missing test coverage, code smelling for refactor, outdated docs, an inadvertent breaking API change, a flaky CI step — **don't just comment or fix it silently.** Instead:

1. Open a `maintenance-task` sub-issue using the **"Maintenance task" issue template** (`.github/ISSUE_TEMPLATE/maintenance-task.yaml`).
2. Pick the matching category in the dropdown.
3. Fill in the Scope and Acceptance fields with concrete observable outcomes.
4. After creation, add the corresponding `maintenance - <category>` label (the form pre-fills only `maintenance-task`; the category must be added manually).
5. If you're submitting a PR that addresses the issue, include `Fixes #<sub-issue-number>` in the PR body so the project board auto-marks the item as Done.

**Reserve plain (non-`maintenance - ` prefixed) issues for product decisions**: stories, epics, one-off bug fixes.

## Agent Guidelines

### Trust These Instructions
This information was validated against the repository on 2026-10-10. **Only search for additional information if these instructions are incomplete or found to be incorrect.**

### When Working in This Repository
1. **Scope**: one story per PR, and a story PR's title carries the story ID (`E27.3: …`); a maintenance or review-fix PR names the issue it closes in its body instead. Config-only changes (protected files) go in their own PR.
2. **Adding Dependencies**: `dotnet add package`; a dependency note in the PR (`pr-gate` skill), and the license must be on the allow-list (`.github/license-audit/`).
3. **Code Style**: follow `.editorconfig` (file-scoped namespaces, `var` where the type is apparent, Allman braces) and `docs/CODING-CONVENTIONS.md`.
4. **Testing**: unit tests in `Wolfgang.Wms.UnitTests`, database and host tests in `Wolfgang.Wms.IntegrationTests` with a SQL Server and a PostgreSQL twin for every database behaviour; test names read `Method_when_condition_expected_result`.
5. **Coverage**: 90 % on `src/` (Domain 100 %), 100 % on test assemblies, per assembly.
6. **Changelog and docs**: a fragment for any PR that touches `src/`; a docs change with every `feature` or `breaking` fragment.
7. **Schema**: a model change ships with a migration per provider and passes `Check-Migrations.ps1`; never let the API migrate.
8. **API**: a changed endpoint updates `docs/api/openapi-v0.json` and the generated client (`scripts/Update-ApiClient.ps1`); breaking changes to a frozen version fail the OpenAPI diff.
9. **Security**: review DevSkim / CodeQL / Semgrep / InspectCode findings and fix them; exclude a false positive as narrowly as possible, with a reason, never by lowering the bar.
10. **Maintenance framework**: when you find improvement work that fits a category, open a `maintenance-task` sub-issue rather than fixing silently (see above).

### Validation Steps
Before submitting changes:
1. `pwsh ./scripts/build-pr.ps1` (or `dotnet restore && dotnet build --configuration Release` + `dotnet test --configuration Release` at minimum)
2. Verify coverage meets the gates
3. Add the changelog fragment (and docs) if `src/` changed
4. Run the `pr-gate` skill checklist
5. Ensure all GitHub Actions checks pass — a red `Detect .NET Projects` on a protected-file change is expected and needs a maintainer
