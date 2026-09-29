<!-- Title: "E27.3: Short imperative summary" — the story ID first. One story per PR.
     Config-only PRs (workflows, .editorconfig, Directory.Build.props, BannedSymbols.txt, coverlet.runsettings)
     are separate from code PRs. Checklist: .claude/skills/pr-gate/SKILL.md -->

Refs #<story issue> (E27.3).
<!-- Stacked on #<pr> — merge that first, then restack. (delete if not stacked) -->

## What

<!-- What changed and why, in a paragraph a reviewer can read without the diff. -->

## AC → tests

<!-- Every acceptance criterion of the story this PR covers, and the test that proves it. "doc" for a
     documentation-only AC; "E<n> (reason)" when another epic delivers it. -->

| AC | Test |
|----|------|
|    |      |

## Docs

<!-- Conventions section, ADR, docfx page touched — or "none needed" with a reason. -->

## Migration

<!-- "no migration", or: reviewed for data loss, lock duration, both providers, site_id and row_version on
     new tables, Up/Down round-trip. -->

## Dependencies

<!-- "none", or per new package: why the BCL does not cover it, its license, the interface it sits behind. -->

## Verification

<!-- Local gate summary: build warnings, tests passed, coverage per assembly. -->

## Checklist

- [ ] Title starts with the story ID; one story per PR
- [ ] Every AC above maps to a test (or says which epic delivers it)
- [ ] Docs changed in this PR where behaviour or rules changed
- [ ] `src/` changed → fragment under `changelog/unreleased/` with a type; otherwise `no-changelog` label with a reason
- [ ] Migration reviewed (or "no migration")
- [ ] New dependency has a license note (or "none")
- [ ] Protected configuration files are not bundled with code in this PR
- [ ] Local gate passed: 0 warnings, all tests, coverage at the floors
