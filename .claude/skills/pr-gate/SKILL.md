---
name: pr-gate
description: Author-side checklist for every Wolfgang.Wms pull request (E85.1) — story ID, AC-to-test map, docs, fragment, migration, dependency note. Run before opening or updating a PR.
---

# PR gate (E85.1)

Run this before `gh pr create` and again after any push that changes scope. CI (`pr.yaml`) checks what it can;
this list covers what only the author knows. See [docs/DELIVERY-PROCESS.md](../../../docs/DELIVERY-PROCESS.md).

## 1. Title carries the story ID

`E27.3: Short imperative summary`. One story per PR. A PR that is part of a story says which part
(`E27.3 (config): …`). Config-only PRs (protected files: workflows, `.editorconfig`, `Directory.Build.props`,
`BannedSymbols.txt`, `coverlet.runsettings`) are separate from code PRs — the guard fails a mixed PR.

## 2. Every acceptance criterion maps to a test

In the PR body, a table: AC → test name(s) (or "doc" for a documentation-only AC, or the epic that will
deliver it with a reason). An AC without a test is either untested or wrongly scoped; say which.
Architecture rules get a **positive control** (a fixture the test must flag) next to the real assertion.

## 3. Docs in the same PR

- New or changed public surface: XML docs on the members (`CS1591` fails the build anyway) and the matching
  section of `docs/CODING-CONVENTIONS.md` or the ADR under `docs/adr/` when a rule changed.
- A decision with alternatives: an ADR (`docs/adr/NNNN-title.md`, Status/Date/Context/Decision/Consequences).
- User-facing behaviour: the docfx page that describes it.

## 4. Changelog fragment with a type

Any change under `src/` needs `changelog/unreleased/<change-name>.md` — line 1 `type: breaking|feature|fix|docs|internal`,
then one paragraph a customer can read. No `src/` change → `no-changelog` label with a reason in the PR body.
A `breaking` fragment says whether the break is to the HTTP API contract, binary compatibility, or both.

## 5. Migration reviewed

If the PR adds an EF migration: reviewed for data loss, lock duration on large tables, both providers,
`site_id` on every new site-scoped table, `row_version` on every new table, and an `Up`/`Down` that round-trips.
State the review in the PR body. No migration → say "no migration".

## 6. New dependency has a license note

A new `PackageReference` outside the BCL, Microsoft and Chris-Wolfgang libraries needs a sentence in the PR
body: why the BCL does not cover it, its license (allow-list: MIT, Apache-2.0, BSD, MPL-2.0), and the interface
it sits behind. `DependencyPolicyTests` rejects the deny list; the note is for everything else.

## 7. Local gate before push

`scripts/build-pr.ps1` (or the session's equivalent) must pass: build with 0 warnings, all tests, coverage at
the floors (90 % `src/`, 100 % `tests/`, Domain 100 %). Paste the summary line in the PR body.

## 8. PR body template

```
Refs #<story issue> (E27.3).
Stacked on #<pr> — merge that first, then restack.   (only when stacked)

## What
## AC → tests
| AC | Test |
## Docs
## Migration
## Dependencies
## Verification
```
