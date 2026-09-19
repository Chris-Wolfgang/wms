# Delivery process (E85)

The path from pull request to release, and the rules each step enforces. The checklists that a session or a
developer runs at each gate are versioned next to the code under [`.claude/skills/`](../.claude/skills/) (E85.6);
this page is the map, [CONTRIBUTING.md](../CONTRIBUTING.md) is the human summary.

| Gate | Trigger | Skill | Workflow |
|------|---------|-------|----------|
| PR gate (E85.1) | every pull request | `pr-gate` | `pr.yaml` |
| Main gate (E85.2) | squash-merge to `main` | — | `release.yaml` dev mode (E83) |
| Prerelease gate (E85.3) | tag `vX.Y.Z-rc.N` | `prerelease-review` | `release.yaml` prerelease |
| Release gate (E85.4) | tag `vX.Y.Z` | `release` | `release.yaml` release mode |
| Hotfix gate (E85.5) | fix on `release/N.x` | `hotfix` | same `pr.yaml` / `release.yaml` |

## Branching (E85.2)

Trunk-based. `main` is always releasable. A change is a short-lived branch named `feature/E27.3-short-name`
(or `fix/…`, `docs/…`, `config/…`), one pull request, squash-merged. There is no `develop` branch. Linear
history is required on `main` (ruleset "Protect main branch"), so stacked pull requests are restacked with
`scripts/restack.ps1` after each merge ([docs/STACKED-PRS.md](STACKED-PRS.md)). Every workflow builds and
tests the **Release** configuration; Debug is never used in CI.

## PR gate (E85.1)

`pr.yaml` runs on every pull request: secrets scan (gitleaks), protected-file guard ("Detect .NET Projects"),
changelog-fragment check, ReSharper InspectCode, Stage 1 (Linux build + tests + coverage gate: 90 % on `src/`,
100 % on `tests/`, `COVERAGE_FLOORS` per assembly, Domain at 100 %), Stage 2 (Windows), security scans
(DevSkim, CodeQL). The architecture tests (`Wolfgang.Wms.UnitTests/Architecture`) run inside Stage 1 and 2, so a
dependency, purity, placement, or time-convention violation fails the build rather than a review. `license-audit`,
`semgrep`, `sourcelink` and `actions-audit` run on the `pull_request` trigger alongside.

The skill `pr-gate` is the author's side of the same gate: story ID in the title, every acceptance criterion
mapped to a test, docs in the same PR, a changelog fragment with a type, a reviewed migration, a license note for
any new dependency.

Target: under 10 minutes wall-clock, without sacrificing correctness. Jobs already run in parallel where they
have no dependency; slow checks (full simulator load, benchmarks, AOT publish, compose smoke, Entra, full
Playwright + axe) run on `main` merges and nightly, never per PR. A `paths:` filter is never put on a job that is
a **required** status check (a PR the filter skips would wait forever for it); path-based skipping is done
inside the job with `dorny/paths-filter`-style detection, or the job is not required.

Not yet wired, tracked with the epic that introduces the thing being checked: tests on both database providers
and migration drift (E2), the OpenAPI diff comment (first API endpoint), Playwright + axe (E82).

## Main gate (E85.2)

A squash-merge to `main` runs `release.yaml` in **dev mode**: same build, images, installers, SBOM, dev-identity
signing, docs published under `/dev/`, simulator smoke load. Versions are MinVer heights,
`0.x.y-alpha.0.N`. Dev mode changes only the publish destination, the signing identity and the version
suffix; the pipeline is the release pipeline, exercised on every merge so release day holds no surprises. The
image, installer and signing stages arrive with E83; until then the dev-mode run is the template's
validate-and-pack path.

## Versioning (E85.4)

- **SemVer from 0.1.0.** While the product is below 1.0: `breaking` → minor, `feature`/`fix` → patch. From
  1.0.0: `breaking` → major, `feature` → minor, `fix` → patch. `scripts/changelog.ps1 bump` computes the next
  version from the fragment types; the tag is the version (`v0.4.2`).
- **MinVer stamps `FileVersion` and `InformationalVersion`** from the git tag (`MinVerTagPrefix=v`,
  `MinVerDefaultPreReleaseIdentifiers=alpha.0`, `MinVerMinimumMajorMinor=0.1`). Builds between tags are
  `0.x.y-alpha.0.N` where N is the commit height. Nothing in a csproj carries a version.
- **`AssemblyVersion` is pinned** in `Directory.Build.props` (`WmsAssemblyVersion`), starting at `0.1.0.0`. It
  changes only when **binary compatibility** breaks: a consumer compiled against Domain, Core or Client can no
  longer drop in the new DLL and must recompile. It then jumps to the version of the release that broke it
  (`0.1.0.0` → `0.27.0.0`, later `2.0.0.0`) so the number identifies the breaking release. A `breaking`
  fragment states whether the break is to the HTTP API contract, to binary compatibility, or both.
- **Android** derives `versionCode` from the tag as `major*10000 + minor*100 + patch` and `versionName` as the
  SemVer string (target `WmsAndroidVersionFromTag` in the Android csproj, after MinVer).
- **Milestones** start at `v0.1.0`; each build phase closes as a `0.x` minor; `v1.0.0` is the first stable,
  marketable release. The `release` skill closes or renames the milestone and creates the next one.

## Prerelease gate (E85.3)

Tag `vX.Y.Z-rc.N` (a GitHub pre-release). `release.yaml` publishes docs and images for the candidate, runs the
full simulator suite and the Entra ID integration test (E11.5). Before the tag, the `prerelease-review` skill
reads everything since the last release: concurrency, idempotency, migrations against real data, API
compatibility, security, simulator coverage, docs versus closed stories, screenshots. Findings become issues,
not notes.

## Release gate (E85.4)

Tag `vX.Y.Z` (a GitHub release, immutable). The same `release.yaml` in **release mode**: build, real signing
identity, images, installers and docs published under `/vX.Y/`, GitHub release with the assembled changelog,
SBOM attached (E85.8). The `release` skill assembles the changelog (`changelog.ps1 assemble`), writes migration
notes, runs the upgrade test from the previous version, regenerates notices, closes the milestone and creates
the next one.

## Hotfix gate (E85.5)

- **`main` still shippable:** the hotfix is a PR to `main` plus a patch tag. Nothing else.
- **Older major:** create `release/N.x` lazily from the latest `vN.x.y` tag; fix on that branch by PR (squash,
  same `pr.yaml`); tag `vN.x.y` there; docs publish under that version. Release branches carry the same ruleset
  as `main` and accept `fix` and `docs` fragments only: a `feature` fragment fails CI.
- **Forward flow:** merge `release/N.x` into `main` as a true merge commit, the one exception to squash-only,
  so the fix is never re-applied by hand. Conflicts are resolved on `main`, keeping `main`'s version where
  `main` already fixed the same thing differently. A release tag fails CI when its branch has commits not yet
  merged to `main`.
- **Support window** is defined at 1.0 (default: the previous major receives fixes for a set period after the
  new major ships). `0.x` moves forward only.

## Upstream gaps (E85.7)

When a story needs something a Chris-Wolfgang library lacks, the gap is raised **upstream** as an issue on that
library from its "Upstream gap" issue template, quoting the WMS story ID; the WMS story lists that issue under
"Depends on". The WMS never accumulates a workaround for a library gap without the upstream issue existing.

## Hardening and supply chain (E85.8, E85.9)

Every `uses:` is pinned to a commit SHA with a version comment and Dependabot updates them weekly;
every workflow declares `permissions:`; the repository's default token is read-only. A CycloneDX SBOM is
generated on every `main` push (`sbom.yaml`) and, from E83, attached to the release and the images, with
`THIRD-PARTY-NOTICES.md` derived from it; images are signed with cosign (keyless, GitHub OIDC) and Windows
installers are Authenticode-signed.
