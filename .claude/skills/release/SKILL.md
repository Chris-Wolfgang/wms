---
name: release
description: Cut a Wolfgang.Wms release (E85.4) — version from fragments, changelog assembled, migration notes, upgrade test, notices, milestone rolled, tag. Chris creates the GitHub release; this skill prepares everything up to the tag.
---

# Release (E85.4)

Preconditions: the `prerelease-review` findings are closed or deferred with a reason; `main` is green; the
release candidate `vX.Y.Z-rc.N` passed `release.yaml`. See
[docs/DELIVERY-PROCESS.md](../../../docs/DELIVERY-PROCESS.md).

## 1. Version

`pwsh scripts/changelog.ps1 bump` prints the next version from the fragment types under
`changelog/unreleased/`. Below 1.0: `breaking` → minor, `feature`/`fix` → patch; from 1.0: `breaking` → major,
`feature` → minor, `fix` → patch. Nothing in a csproj carries a version; MinVer reads the tag.

If any `breaking` fragment names **binary compatibility**, bump `WmsAssemblyVersion` in
`Directory.Build.props` to `X.Y.Z.0` of this release (a config-only PR, merged before the tag).

## 2. Changelog and notes

- `pwsh scripts/changelog.ps1 assemble -Version X.Y.Z` writes the new section into `CHANGELOG.md` and deletes
  the fragments. Review the wording as release notes, not commit messages.
- Migration notes: for every migration since the last release, what it changes, expected duration on a
  large site, and any manual step. Goes under the release section as "Upgrading".
- Regenerate notices: `pwsh scripts/Generate-ThirdPartyNotices.ps1`; commit `THIRD-PARTY-NOTICES.md` if changed.

One PR ("Release X.Y.Z") carries the changelog, notes and notices. Squash-merge it.

## 3. Upgrade test

Install the previous release from its published artifacts on a clean environment with seeded data, upgrade to
the candidate build, run the simulator smoke load, verify data and settings survived. Record the result in the
release PR.

## 4. Milestone

Close (or rename to the final version) the current milestone; create the next one (`v0.(x+1).0`); move open
issues forward with a comment.

## 5. Tag and release

Push the annotated tag `vX.Y.Z` on the release PR's merge commit. Chris creates the GitHub release from the
tag (immutable release, assembled changelog as the body); `release.yaml` runs in release mode and attaches
the artifacts and SBOM. Do not create the GitHub release from a session.

## 6. After

Verify the release run: artifacts present, docs published under `/vX.Y/`, images signed. Post-release, bump
any baseline the repo keeps (coverage floors, API compat baseline) in a follow-up PR.
