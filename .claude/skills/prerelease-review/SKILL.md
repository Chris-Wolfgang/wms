---
name: prerelease-review
description: Full review of everything since the last release before tagging a Wolfgang.Wms release candidate (E85.3). Findings become issues, not notes.
---

# Prerelease review (E85.3)

Run before tagging `vX.Y.Z-rc.N`. Input: every merged PR since the last release tag
(`git log <last-tag>..main --merges` or the squash commits) and the open fragments under `changelog/unreleased/`.
Output: one issue per finding, labelled `prerelease` and the milestone, linked from a summary comment on the
milestone. See [docs/DELIVERY-PROCESS.md](../../../docs/DELIVERY-PROCESS.md).

## Read every change for

1. **Concurrency.** Leases, replays and any read-modify-write on shared rows: is the update conditional on
   `row_version`, is the retry bounded, can two devices win the same task?
2. **Idempotency.** Every endpoint a device can retry (deposit, pick confirm, sync): same request twice gives
   the same outcome and one side effect; the outbox key is stable.
3. **Migrations on real data.** Each migration applied to a copy of a production-shaped database on both
   providers: duration, locks, nullability of new columns against existing rows, `Down` round-trip.
4. **API compatibility.** OpenAPI diff since the last release: removed or renamed members, tightened
   validation, changed status codes. Anything breaking has a `breaking` fragment naming the contract.
5. **Security.** New endpoints carry a permission; new settings have a scope; secrets never logged; new
   dependency licenses noted; Dependabot and code-scanning alerts at zero or dispositioned.
6. **Simulator coverage.** Every new device flow has a simulator scenario; the full simulator suite passed on
   the candidate build, not on `main` before the last merge.
7. **Docs versus closed stories.** Each story closed this cycle has its behaviour documented where a user would
   look; screenshots updated where the UI changed.
8. **Fragments.** Every fragment reads as a release note a customer understands; types are right; nothing
   merged without one that should have had one.

## Then

- Fix-forward findings go in as PRs before the rc tag; anything else is an issue with the milestone and a
  severity label. A release candidate is not tagged with an open `severity:blocker` finding.
- Record the review as a comment on the milestone: what was read, what was found, what was deferred and why.
