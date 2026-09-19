# Coding conventions

The build enforces these; this page explains them so a review never has to. Story: E1.3 (with E1.7 async and
immutability rules and E1.8 dependency policy referenced where they overlap).

## Enforced by the build

| Rule | How it is enforced |
|------|--------------------|
| Nullable reference types on in every project | `Directory.Build.props` (`<Nullable>enable</Nullable>`) |
| Analyzer violations fail the build, in every configuration | seven analyzers + `TreatWarningsAsErrors` in `Directory.Build.props` (E1.2) |
| Async methods end in `Async` | `VSTHRD200` at `warning` in `.editorconfig`; test projects opt out in `tests/.editorconfig` |
| No sync-over-async, no blocking waits | AsyncFixer, VS Threading analyzers, `BannedSymbols.txt` (`.Result`, `.Wait()`) (E1.7) |
| Every source file carries the license header | `file_header_template` + `IDE0073` in `.editorconfig` (E1.6) |
| Conversion methods live in the layer that knows both types | `ConversionMethodPlacementTests` in `Wolfgang.Wms.UnitTests` (see below) |

## Warning suppressions

A suppression is a decision, and the decision has to be readable next to the code it affects.

- **Targeted:** `#pragma warning disable XXXX` / `restore` around the specific lines, or `[SuppressMessage(..., Justification = "...")]` on the member. Either form needs a one-line reason; a bare suppression is rejected at review.
- **Global** (`.editorconfig` / `.globalconfig` severity changes, `<NoWarn>`): only with a documented reason in the file itself, reviewed at the PR gate because those files are protected. The list is expected to stay very short; prefer fixing the code.
- Never suppress to make a build pass under time pressure. Fix, or open an issue and suppress with its number in the justification.

## Naming

- **Async methods** end in `Async` (analyzer-enforced). Test methods are excluded.
- **Test methods** are `snake_case` sentences that describe behaviour, for example
  `Domain_assembly_references_neither_EF_Core_nor_ASP_NET_nor_MAUI`. The name is the documentation.
  XML doc comments on tests are optional and used only when they add something the name cannot carry:
  setup rationale, the bug being pinned, a link to the story. Never restate the name. The missing-doc
  analyzer is off for test projects.
- **Type conversions** are extension methods named `ToXxx()` / `FromXxx()` for the lower-level type, defined in
  the higher layer that knows both types:

  | Conversion | Lives in | Never in |
  |------------|----------|----------|
  | `ToDto()` / `FromDto()` | `Wolfgang.Wms.Api` | Domain, Infrastructure |
  | `ToEntity()` / `FromEntity()` | `Wolfgang.Wms.Infrastructure` | Domain, Api |

  `Wolfgang.Wms.Domain` knows neither DTOs nor entities and defines none of these. The direction is enforced by
  `ConversionMethodPlacementTests`.

## Domain rules (E1.4)

`Wolfgang.Wms.Domain` is shared with the handheld, so a rule the device applies is byte-for-byte the rule the
server applies. To keep that true:

- Rules are **pure functions**: static methods under `Wolfgang.Wms.Domain.Rules` that take values and return
  values, with no I/O, no clock, no randomness, no static mutable state. Anything a rule needs (the current
  time, a setting) is passed in.
- Expected failures return `Result<T>` / `Result` from `Wolfgang.TryPattern` (E1.7); exceptions are for bugs.
- Rules stay **synchronous**. Domain has no `Task`/`ValueTask`-returning members.
- **One test class per rule** in `Wolfgang.Wms.UnitTests`, covering each rule independently.
- Purity is enforced by `DomainPurityTests`: Domain may reference only an allow-listed set of assemblies, and an
  IL scan rejects calls into `System.IO`, `System.Net`, `System.Data`, `System.Diagnostics.Process`, and
  `System.Threading.Tasks` from any Domain method.

## Records and classes (E1.7)

DTOs, requests, responses and journal events are `record` types: immutable, `with` for copy-and-change, value
equality. EF entities and sqlite-net table models are classes because their frameworks need mutation and
parameterless constructors.

## Dependencies (E1.8)

Prefer the BCL and Microsoft packages. Any other package needs a written reason in the PR and sits behind an
interface this repository owns. Chris-Wolfgang libraries are exempt. Deny list: MediatR, AutoMapper, Moq,
FluentAssertions, Hangfire, MassTransit. Tests use xUnit with built-in asserts; NSubstitute for infrastructure
boundaries; hand-written fakes for Domain interfaces.
