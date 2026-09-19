---
name: hotfix
description: Ship a fix for a released Wolfgang.Wms version (E85.5) — patch tag on main when main is shippable, otherwise a lazily created release/N.x branch, forward-merged as a true merge commit.
---

# Hotfix (E85.5)

See [docs/DELIVERY-PROCESS.md](../../../docs/DELIVERY-PROCESS.md#hotfix-gate-e855).

## Decide the path

- **`main` is still shippable** (nothing merged since the last release that a customer must not get yet):
  the fix is an ordinary PR to `main` through `pr-gate`, then the `release` skill for a patch version. Stop here.
- **Customer is on an older major** or `main` carries unreleased breaking work: use a release branch.

## Release branch

1. `release/N.x` exists? If not, create it from the latest `vN.x.y` tag:
   `git checkout -b release/N.x vN.x.y && git push -u origin release/N.x`. Apply the `main` ruleset to it
   (`scripts/Setup-BranchRuleset.ps1` with the branch pattern) before the first PR.
2. Branch `fix/E27.3-short-name` from `release/N.x`; fix; fragment of type `fix` or `docs` only (a `feature`
   fragment fails CI on a release branch); PR targets `release/N.x`; same `pr.yaml`, squash-merge.
3. Run the `release` skill against the branch: version is `N.x.(y+1)`, tag `vN.x.(y+1)` on the branch;
   docs publish under that version.

## Flow the fix forward

4. Merge `release/N.x` into `main` with a **true merge commit** (`git merge --no-ff release/N.x`), the one
   exception to squash-only, so the fix is never re-applied by hand. Resolve conflicts on `main`; where `main`
   already fixed the same thing differently, keep `main`'s version. Open that merge as a PR so CI runs; the
   merge is done by an admin with the linear-history rule bypassed for this one merge.
5. A release tag on `release/N.x` fails CI while the branch has commits not yet merged to `main`; do step 4
   before the next tag, never after.

## Support window

Defined at 1.0: the previous major receives fixes for a set period after the new major ships. `0.x` moves
forward only; there are no `release/0.x` branches.
