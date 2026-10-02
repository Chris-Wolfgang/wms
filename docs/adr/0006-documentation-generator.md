# ADR 0006: DocFX for the versioned docs site; references generated from the product

**Status:** Accepted (E83.0) · **Date:** 2026-09-20

## Context

The manuals serve non-IT readers (pickers, supervisors) as well as integrators and administrators (E83.0).
They must be published per version so a reader sees the manual for the release they run (E83.1), change in
the same pull request as the code (E83.2), carry generated settings, permission, error-code and API
references so nothing is ever missing (E83.3), and be packaged into the release for the console's local
`/help/` (E83.4). The fleet's repositories already publish with DocFX through a shared workflow
(`docfx.yaml`) with a version picker (docs/DOCFX-VERSION-PICKER.md).

## Options weighed

| | DocFX (modern template) | MkDocs Material | Docusaurus |
|---|---|---|---|
| Version switching | Per-tag build under `/versions/vX.Y.Z/` with a `latest` alias and the fleet's picker, already wired | `mike` plugin, comparable | Built-in versioning, comparable |
| Non-IT readability | Modern template: search, dark mode, clean article layout; no framework knowledge needed to author Markdown | Excellent; Python toolchain to add to CI | Excellent; Node toolchain, React for anything custom |
| .NET API reference | Native: metadata from the `csproj` files into the same site and search index | Needs a separate generator and a stitched site | Same |
| Build time / toolchain | One `dotnet tool`, no extra runtime in CI; already in the release workflow | Python + pip pins (`--require-hashes`) | Node + npm lockfile |
| Generated references | Any Markdown, so the pages the product generates drop in | Same | Same |

## Decision

- **DocFX, modern template**, as already published by `docfx.yaml`: versioned under `/versions/<tag>/`
  with `latest`, the version picker in the header, the .NET API reference from the source projects in the
  same site. Rejected: MkDocs Material and Docusaurus add a second toolchain to CI for no gain on the
  criteria above; the API reference integration is what tips it.
- **The site's source is `docfx_project/`** (articles under `docs/`, references under `docs/reference/`).
  `docs/` at the repository root stays the engineering guides (conventions, delivery process, ADRs); customer-
  facing pages move to the site as they are written.
- **References are generated from the product** (`Wolfgang.Wms.Core.Docs.ReferencePages`) from the same
  registries the host serves — settings, permissions, error codes, modules — plus the OpenAPI document
  (`docs/api/openapi-v0.json`) and the licensing comparison. Each is committed and a test fails when the
  committed copy is stale, so the diff is reviewed in the pull request that changes the product (E83.2, E83.3).
- **Error codes link into `troubleshooting.md`** by anchor (`ApiProblems.DocsBase + "#" + DocsAnchor`);
  `ReferenceDocsTests` fails when a code has no entry.
- **Local help (E83.4)**: the release packages the built site next to the console under `help/`; the console
  serves it at `/help/` when present (`ConsoleHelp`), so links match the installed version by construction and
  work air-gapped. `HelpLinks` builds the paths the console and the handheld use.

## Consequences

- Authors write Markdown only; a new setting, permission or code without its docs fails the build.
- Screenshots and walkthroughs (E83.6) are a later addition to the same site; the prerelease docs review
  (E83.5) checks the closed stories against the pages under `docfx_project/docs/`.
- The nightly `docs-check.yaml` builds the site and checks links; a per-PR link check is a workflow change
  (protected file) to schedule separately.
