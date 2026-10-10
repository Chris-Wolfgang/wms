# Wolfgang.Wms

Warehouse management system (Wolfgang.Wms): an installed server with a web console and a handheld app, starting with the picking module.

[![PR build](https://img.shields.io/github/actions/workflow/status/Chris-Wolfgang/wms/pr.yaml?event=pull_request_target&label=PR%20build&logo=github)](https://github.com/Chris-Wolfgang/wms/actions/workflows/pr.yaml)
[![release](https://img.shields.io/github/actions/workflow/status/Chris-Wolfgang/wms/release.yaml?event=release&label=release&logo=github)](https://github.com/Chris-Wolfgang/wms/actions/workflows/release.yaml)
[![License: TBD](https://img.shields.io/badge/License-TBD-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-purple.svg)](https://dotnet.microsoft.com/)
[![GitHub](https://img.shields.io/badge/GitHub-Repository-181717?logo=github)](https://github.com/Chris-Wolfgang/wms)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/Chris-Wolfgang/wms/badge)](https://scorecard.dev/viewer/?uri=github.com/Chris-Wolfgang/wms)
[![Coverage](https://img.shields.io/endpoint?url=https://Chris-Wolfgang.github.io/wms/versions/latest/coverage/badge.json&logo=github)](https://Chris-Wolfgang.github.io/wms/versions/latest/coverage/)

---

## 📦 Getting it running

Wolfgang.Wms is an application you install, not a library you reference. An install is a database, the API, the
web console and, on the floor, the handheld app:

1. **Create the database** on SQL Server 2022+ (Express included) or PostgreSQL 16+ and point the API at it with
   `Wms:Database:Provider` / `Wms:Database:ConnectionString` ([docs/CONFIGURATION.md](docs/CONFIGURATION.md)).
2. **Apply the schema with `wms-migrate`** using the DBA's rights; the API's own service account never changes the
   schema and refuses to start while it is behind or ahead ([docs/MIGRATE.md](docs/MIGRATE.md)).
3. **Start the API** and check `GET /api/v0/system/schema` reports the schema up to date
   ([docs/BOOTSTRAP.md](docs/BOOTSTRAP.md)); then open the console.

The only NuGet package is `Wolfgang.Wms.Client`, the generated API client for devices and integrations, published
with each release; the server itself ships as published hosts, not packages.

---

## 📄 License

This project is **not yet licensed**. All rights reserved pending license selection: no reuse, redistribution, or hosting rights are granted. See the [LICENSE](LICENSE) file.

---

## 📚 Documentation

- **GitHub Repository:** [https://github.com/Chris-Wolfgang/wms](https://github.com/Chris-Wolfgang/wms)
- **API Documentation:** https://Chris-Wolfgang.github.io/wms/
- **Formatting Guide:** [docs/README-FORMATTING.md](docs/README-FORMATTING.md)
- **Contributing Guide:** [CONTRIBUTING.md](CONTRIBUTING.md)

---

## 🚀 Quick Start

The shortest path on a development machine, with a database already created:

```bash
# the schema, with the DBA's connection string (SqlServer or PostgreSql)
dotnet run --project src/Wolfgang.Wms.Migrate -- --provider SqlServer --connection-string "<connection string>"
dotnet run --project src/Wolfgang.Wms.Migrate -- --status --provider SqlServer --connection-string "<connection string>"

# the API (reads Wms:Database from appsettings or the environment); GET /api/v0/system/schema says upToDate
dotnet run --project src/Wolfgang.Wms.Api

# the console; / lists the workspaces you can enter
dotnet run --project src/Wolfgang.Wms.Web
```

A pull request's full gate runs locally with `pwsh ./scripts/build-pr.ps1`; the SQL Server integration tests use
a local instance when `WMS_TEST_SQLSERVER` names one (Express LocalDB works) and containers otherwise.

---

## ✨ Features

| Area | What is there today |
|------|---------------------|
| Modular monolith | One API, one database, one deployable; modules register endpoints, keys and screens through one contract, and the picking module is the first |
| Console | A Blazor Server console with five workspaces (Configure, Supervise, Resolve, Report, Insights), each a license feature and a permission; scan-first screens take tethered-scanner input anywhere |
| Handheld | An Android (.NET MAUI) app for the floor, held to a minimum version the server publishes |
| Two database engines | SQL Server 2022+ (Express included) and PostgreSQL 16+ from one model, with per-provider migrations and a conventions test that proves the schemas match |
| `wms-migrate` | The one way the schema is created, upgraded, scripted or rolled back; data-losing downgrades need confirmation; the API never migrates |
| API | Versioned (`/api/v0`), OpenAPI document committed and diffed on every PR, a generated Kiota client, keyset paging, ETags and `If-Match` concurrency, idempotency keys, problem details with stable error codes |
| Your identifiers | Customer-supplied identifiers with mask and regex formats and GS1 structural validation, so a bad label is rejected before it reaches a screen |
| Read models | Per-instance caches invalidated by row version, and ETags derived from the same version, so a hot read costs nothing between polls |

---

## 🎯 Runtime and databases

| Component | Runs on |
|-----------|---------|
| API, console, worker, `wms-migrate` | .NET 10 runtime, Windows or Linux |
| Handheld app | Android (`net10.0-android`) |
| Database | SQL Server 2022 or later (Express included), or PostgreSQL 16 or later |
| `Wolfgang.Wms.Client` (NuGet) | .NET 10 |

---

## 🔍 Code Quality & Static Analysis

This project enforces **strict code quality standards** through **7 specialized analyzers**, ReSharper InspectCode on every PR, and custom async-first rules:

### Analyzers in Use

1. **Microsoft.CodeAnalysis.NetAnalyzers** - Built-in .NET analyzers for correctness and performance
2. **Roslynator.Analyzers** - Advanced refactoring and code quality rules
3. **AsyncFixer** - Async/await best practices and anti-pattern detection
4. **Microsoft.VisualStudio.Threading.Analyzers** - Thread safety and async patterns
5. **Microsoft.CodeAnalysis.BannedApiAnalyzers** - Prevents usage of banned synchronous APIs
6. **Meziantou.Analyzer** - Comprehensive code quality rules
7. **SonarAnalyzer.CSharp** - Industry-standard code analysis

### Async-First Enforcement

This library uses **`BannedSymbols.txt`** to prohibit synchronous APIs and enforce async-first patterns:

**Blocked APIs Include:**
- ❌ `Task.Wait()`, `Task.Result` - Use `await` instead
- ❌ `Thread.Sleep()` - Use `await Task.Delay()` instead
- ❌ Synchronous file I/O (`File.ReadAllText`) - Use async versions
- ❌ Synchronous stream operations - Use `ReadAsync()`, `WriteAsync()`
- ❌ `Parallel.For/ForEach` - Use `Task.WhenAll()` or `Parallel.ForEachAsync()`
- ❌ Obsolete APIs (`WebClient`, `BinaryFormatter`)

**Why?** To ensure all code is **truly async** and **non-blocking** for optimal performance in async contexts.

---

## 🛠️ Building from Source

### Prerequisites
- [.NET SDK](https://dotnet.microsoft.com/download) - the current release (10.0); see *Supported Frameworks* for the targets that are built
- [PowerShell 7](https://github.com/PowerShell/PowerShell) (`pwsh`) for the scripts under `scripts/`

### Build Steps

```bash
# Clone the repository
git clone https://github.com/Chris-Wolfgang/wms.git
cd wms

# Restore dependencies
dotnet restore

# Build the solution
dotnet build --configuration Release

# Run tests
dotnet test --configuration Release

# Run code formatting
pwsh ./scripts/format.ps1

# Run the PR workflow's Windows stage locally (build, tests on every TFM, coverage gates, DevSkim, gitleaks)
pwsh ./scripts/build-pr.ps1
```

### Code Formatting

This project uses `.editorconfig` and `dotnet format`:

```bash
# Format code
dotnet format

# Verify formatting without changing files
dotnet format --verify-no-changes
```

See [docs/README-FORMATTING.md](docs/README-FORMATTING.md) for detailed formatting guidelines.

### Building Documentation

This project uses [DocFX](https://dotnet.github.io/docfx/) to generate API documentation:

```bash
# Install DocFX (one-time setup)
dotnet tool install -g docfx

# Generate API metadata and build documentation
cd docfx_project
docfx metadata  # Extract API metadata from source code
docfx build     # Build HTML documentation into docfx_project/_site/
```

The documentation is built and deployed to GitHub Pages by the release workflow: each release lands under `versions/<tag>/` (plus `versions/latest/`) with a version picker on every page, so earlier versions stay online.

**Local Preview:**
```bash
# Serve documentation locally (with live reload)
cd docfx_project
docfx build --serve

# Open http://localhost:8080 in your browser
```

**Documentation Structure:**
- `docfx_project/` - DocFX configuration and source files (`_site/` is the local build output, not committed)
- `docs/` - Repository guides (workflow security, release setup, stacked PRs, ...) - not the generated site, which lives on the `gh-pages` branch
- `docfx_project/index.md` - Main landing page content
- `docfx_project/docs/` - Additional documentation articles
- `docfx_project/api/` - Auto-generated API reference YAML files

---

## 🔐 Verify a Release

The `Wolfgang.Wms.Client` package published from this repository carries a [SLSA build-provenance attestation](https://docs.github.com/en/actions/security-for-github-actions/using-artifact-attestations/using-artifact-attestations-to-establish-provenance-for-builds): a signed statement, recorded on GitHub, that the exact `.nupkg` bytes were produced by this repository's release workflow at a given commit. Verify a downloaded package with the GitHub CLI:

```bash
gh attestation verify Wolfgang.Wms.Client.X.Y.Z.nupkg \
  --repo Chris-Wolfgang/wms \
  --signer-workflow Chris-Wolfgang/wms/.github/workflows/release.yaml
```

`--repo` restricts the lookup to this repository's attestations and `--signer-workflow` requires that the signing workflow was this repository's `release.yaml`; with both, the command fails if the package was built anywhere else or was modified after the build. A CycloneDX SBOM (`*.bom.json`) listing the package's full dependency closure is attached to each GitHub Release alongside the package.

---

## 🔄 Keeping Up With the Template

This repository was generated from [Chris-Wolfgang/repo-template](https://github.com/Chris-Wolfgang/repo-template); `.template-version` records the template commit it was set up from. To pull later template fixes (workflows, analyzer rules, tool pins):

```powershell
pwsh ./scripts/upgrade.ps1          # dry run: safe / review / in-sync / removed per file
pwsh ./scripts/upgrade.ps1 -Apply   # take the safe files, sidecar the rest, re-stamp
```

Files never touched here come across as-is; files customised here get a `<file>.template` sidecar to merge by hand. Workflow and `Directory.Build.props` changes must travel in a configuration-only PR (the guard passes it on review); mixed with code they fail the guard.

## 🤝 Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for:
- Code quality standards
- Build and test instructions
- Pull request guidelines
- Analyzer configuration details

---


## 🙏 Acknowledgments

Built on .NET, ASP.NET Core and Blazor, Entity Framework Core, Kiota, Serilog and Testcontainers; the full list of
third-party components and their licenses is in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
