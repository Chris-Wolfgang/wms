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

## Async rules (E1.7)

A blocked server thread is a picker waiting at a bin, so:

| Rule | Enforced by |
|------|-------------|
| I/O methods are async and return `Task<T>` / `ValueTask<T>` | review; `VSTHRD200` names them `…Async` |
| No sync-over-async: no `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` | `BannedSymbols.txt` (build error) |
| No `async void`, no blocking waits inside async code | `VSTHRD100`/`VSTHRD101`, `VSTHRD002` |
| No `out` parameters on async methods | C# compiler (CS1988) |
| Expected failures return `Result<T>` instead of throwing | `Wolfgang.TryPattern`; review |
| Pure Domain rules stay synchronous | `DomainPurityTests` (no `Task`/`ValueTask` members in Domain) |
| Return the task directly when nothing follows the `await` | `AsyncFixer01` (off in test projects, where `await` is needed for `Assert.ThrowsAsync`) |
| `ValueTask` on hot paths (scan, deposit, task-list) | review; `CA2012` guards misuse |

## Typed keys, no magic strings (E1.13)

Feature flags, setting keys, permissions, license limits and features, error codes, issue types and job names
are each a small key type in `Wolfgang.Wms.Domain.Keys` (`FeatureFlag`, `SettingKey<T>`, `Permission`,
`LicenseLimit`, `LicenseFeature`, `ErrorCode`, `IssueType`, `JobName`):

- Defined once, as `static readonly` instances in a definitions class per module (string + metadata, one
  place only); `KeyDefinitions.Enumerate<TKey>(typeof(PickingPermissions))` builds registries (settings
  pages, the permission catalog, the troubleshooting reference) from those definitions.
- Every API takes the key type, never a string: `IFeatures.IsEnabled(Features.BulkPicking, site)`,
  `ISettings.Get(SettingKeys.LeaseTimeout, scope)` with `T` inferred from the key,
  `ILicense.Check(LicenseLimits.Devices, n)`. String overloads are not written; a reviewer rejects one on
  sight and the module descriptor only accepts the key types.
- Key names are lower-case dotted identifiers (`picking.lease_timeout`) validated at construction.
- Feature flags are settings `feature.<name>` (bool, organisation → site cascade), checked only at edges;
  hidden endpoints return 404; a ship-dark flag names the release that removes it.
- Error codes carry HTTP status, message template, docs anchor and severity; `const` strings only where an
  attribute or `switch` requires one.
- Modules contribute their keys through `ModuleDescriptor.With…()` so the host can enumerate them.

## Data access (E1.11, ADR 0002)

- Single tenant per install: no `tenant_id`. Site separation is `site_id` on every site-scoped entity with an
  EF global query filter from the caller's site context; cross-site reads need `IgnoreQueryFilters()` behind
  a permission and writes outside scope are rejected at `SaveChanges`.
- One `DbContext` per request or job, behind `IUnitOfWork` (`Wolfgang.Wms.Core.Data`): `SaveChangesAsync`
  once per operation, `ExecuteInTransactionAsync` only for the listed multi-step operations and never around
  non-database I/O. No `Set<T>()`, no change tracker.
- Repositories are per aggregate, not per table: `ISkuRepository : IReadOnlyRepository<Sku, SkuId>,
  ISearchableRepository<Sku, SkuCriteria>, IWriteOnlyRepository<Sku>` plus the few business-named methods
  that need EF. Contracts never expose the provider: no `IQueryable`, no `DbContext`, no expression trees;
  search takes a criteria record. `DataAccessConventionTests` scans `Wolfgang.Wms.Core.Data` for leaks.
- Handlers depend on repositories and `IUnitOfWork`, never a context (`DataAccessConventionTests` checks
  constructor parameters of types under `*.Features.*`). Repositories fetch and persist; handlers orchestrate.
- Screens and reports use query classes projecting to records (`AsNoTracking`), not repositories. Handler
  unit tests use hand-written fakes of the repository interfaces.

## Read models and caching (E1.12, ADR 0003)

- Screens and reports read through one query class per read in Infrastructure (`OpenReleasesQuery`): LINQ
  projection with `AsNoTracking` straight to the API record. Hand-written SQL only inside that class when a
  plan requires it; a rollup table only when the raw tables are measurably too slow. Never a repository.
- Caching is per instance and in memory: `VersionedCache<T>` (`Wolfgang.Wms.Core.Caching`) rebuilds its
  value only when `IRowVersionSource` reports a higher `row_version` for the watched tables, probed at most
  once per poll interval. No shared cache component; the database is the truth.
- HTTP caching is validation, never time-based: a resource's `ETag` is its `row_version`
  (`EntityTag.FromRowVersion`), a list's is max `row_version` + count (`EntityTag.FromCollection`), and
  `ConditionalResults.NotModifiedOr` answers a matching `If-None-Match` with `304` from the version column
  alone. API responses send `Cache-Control: private, no-cache` (`CacheControl.Api`); content-hashed static
  assets send `public, max-age=31536000, immutable` (`CacheControl.StaticAsset`). Never `no-store`, never
  `max-age` on data.

## Validation, localization and time (E1.14)

- Request validation is hand-written or DataAnnotations with the source-generated validator
  (`AddValidation()`, AOT-safe); a Domain rule returns `Result<T>` (E1.4), never throws for an expected
  failure. API JSON is camelCase (`ConfigureHttpJsonOptions` in the host); URLs use natural keys where a
  customer would (`/skus/{skuCode}`), surrogate ids where they would not.
- Time is UTC `DateTimeOffset` everywhere except display. No public member of a product assembly exposes
  `DateTime` (`TimeConventionTests`); the clock is an injected `TimeProvider` (`DateTimeOffset.Now/UtcNow` are
  banned symbols); time zones are `TimeZoneInfo` from the site's settings. NodaTime is on the deny list until
  shift math proves `TimeZoneInfo` inadequate, with a written reason.
- Localization exists from day one: `AddWmsLocalization()` / `UseWmsRequestLocalization()`
  (`Wolfgang.Wms.Core.Localization`) register resource-file localizers under each host's `Resources/` folder
  and resolve the request culture from the user's picker cookie, then `Accept-Language`, with parent-culture
  fallback and `Content-Language` on the response. `WmsLocalization.SupportedCultures` is the one list;
  English is the only shipped language in v1. UI text comes from `IStringLocalizer`, never a literal in a
  component; the Blazor stories (E82) add the markup check.

## API versioning and the one API (E82.1, E82.2)

- Every capability is an endpoint under `/api/v{n}/` (`WmsApi.RouteTemplate`, `Wolfgang.Wms.Core.Api`); modules
  map into the group `MapWmsApi()` returns, never onto the app directly. The console and every customer tool
  are clients of the same API; `ConsoleUsesApiOnlyTests` keeps `Wolfgang.Wms.Web` on Domain + the API client
  only (no Core, Infrastructure or EF).
- `v0` is the unstable contract through product 0.x: breaking changes ship in place, each with a `breaking`
  fragment naming the contract. At 1.0, `v1` freezes (`WmsApi.Frozen`); additive changes (new optional fields,
  endpoints, enum values, error codes) stay in-version and clients must ignore unknown fields. See
  [docs/API-VERSIONING.md](API-VERSIONING.md).
- One OpenAPI document per served version at `/openapi/v{n}.json`; the committed copy under `docs/api/` must
  match the served document (`OpenApiDocumentTests`; regenerate with `WMS_UPDATE_OPENAPI=1`).

## API conventions (E82.3)

[docs/API-CONVENTIONS.md](API-CONVENTIONS.md) is the contract; in code (`Wolfgang.Wms.Core.Http`):

- Errors are `ApiProblems.Problem(code, …)` from a typed `ErrorCode`; never `Results.BadRequest("text")`.
- Status codes: 200 read/update/replay, 201 + `Location` + body create (never 3xx), 202 outbox work, 204
  delete/body-less; 409 for an `If-Match` miss, 422 for an `Idempotency-Key` reused with a different body.
- `POST`/`PATCH` handlers honour `Idempotency-Key` (`IdempotencyKey`, `IIdempotencyStore`, `Idempotency.Decide`)
  inside their transaction; the console generates the key when a form opens.
- Lists take `[AsParameters] PageRequest` and return `Page<T>` (keyset, bidirectional `Cursor`, exact
  `total_count`, `min_id`/`max_id`); unbounded tables require a time-range filter.
- Compression is on for JSON/XML/text (`WmsCompression`); authentication endpoints call
  `.DisableResponseCompression()`.

## Console (E82.4)

- One deployable (`Wolfgang.Wms.Web`, Blazor Web App in Server render mode for v1) hosting five workspaces,
  each its own project: `Wolfgang.Wms.Web.Configure`, `.Supervise`, `.Resolve`, `.Report`, `.Insights`
  (`/configure` … `/insights`), plus `Wolfgang.Wms.Web.Shared` (workspace definitions, layout chrome,
  `WorkspaceNav`, `ScanListener`). A workspace project references Domain, the API client and `Web.Shared` only.
- Every workspace is a license feature (`workspace.<name>`) and a permission (`workspace.<name>.enter`)
  defined once in `Workspaces`; the entry gate is `IWorkspaceAccess` consulted by `WorkspaceLayout`; the
  free tier is Configure, Supervise, Resolve and Report; Insights is paid. A single-role user lands in their
  workspace; others pick on `/`.
- Components are render-mode-agnostic: API client only, no server services, no `DbContext`
  (`ConsoleUsesApiOnlyTests`). Switching to WebAssembly/Auto later changes the host, not the components.
- Tethered-scanner input goes through `ScanListener` (keyboard wedge: text then Enter) so every console screen
  applies the same validation and feedback rules as the device (E40).

## API client (E82.8, ADR 0004)

`Wolfgang.Wms.Client` is generated by Kiota from the committed `docs/api/openapi-v0.json`
(`scripts/Update-ApiClient.ps1`, output committed under `Generated/`) and references only the Kiota runtime,
never Domain (`ClientIsolationTests`): client models are the wire contract and Domain records are mapped at
the edge. Every API change regenerates the spec and the client in the same PR. Consumers start from
`WmsApiClient.Create(HttpClient, IAuthenticationProvider?)`; the console workspaces reach the API only through
this client.

## Bootstrap exceptions (E82.5)

The steps that run outside the API are the ones in [docs/BOOTSTRAP.md](BOOTSTRAP.md) (migrate, identity and
first administrator, license install, TLS/proxy, backup/restore) and nothing else; a new one is an ADR. The API's
only bootstrap surface is the read-only `GET /system/schema` (`SchemaModule`, `ISchemaVersionSource`).
API records get a `[JsonSerializable]` line in `WmsJsonContext` so they serialise without reflection.

## Database provider (E2)

The installer picks SQL Server or PostgreSQL at run time: `Wms:Database:Provider` (`SqlServer` | `PostgreSql`;
`None` starts the host without a database for bootstrap only), `Wms:Database:ConnectionString`, and for SQL
Server `Wms:Database:TrustServerCertificate` (see [docs/CONFIGURATION.md](CONFIGURATION.md)). An unknown
provider or a missing connection string fails startup with a message naming the setting. `WmsDbContext`
(`Wolfgang.Wms.Infrastructure.Database`) is the only place a provider is chosen; no provider-specific SQL in
the shared model (E2.3). Migrations are per provider (E2.4): `Wolfgang.Wms.Infrastructure.Migrations.SqlServer`
and `.PostgreSql`, generated together by `scripts/Check-Migrations.ps1 -Add <Name>` and never hand-edited except
for index comments (E3.5); `scripts/Check-Migrations.ps1` (also a CI step) fails when the model changed without
both migrations. A migration that runs against the wrong engine is impossible: each assembly is only ever
loaded by its provider.

## Schema conventions (E3)

[docs/DATABASE-CONVENTIONS.md](DATABASE-CONVENTIONS.md) is the contract: module schemas (never `dbo`/`public`),
snake_case names (`container.zone_group_id`, `pk_`/`fk_`/`ix_`/`ux_` prefixes), server-assigned `long` `id`
keys, no GUIDs, `Restrict` foreign keys with explicit indexes, `decimal(9,3)` quantities, UTC
`DateTimeOffset` timestamps at millisecond precision. `ModelConventions.Apply` enforces the names and types;
`ModelConventions.Verify` is asserted empty by `ModelConventionsTests` for both providers, so a violation
fails the build.

## Records and classes (E1.7)

DTOs, requests, responses and journal events are `record` types: immutable, `with` for copy-and-change, value
equality. EF entities and sqlite-net table models are classes because their frameworks need mutation and
parameterless constructors. Mutable DTOs are rejected at review; a `record` with `init`-only members is the
default shape for anything that crosses the API or the journal.

## AOT, trimming and packaging (E1.9)

- `IsAotCompatible` and `IsTrimmable` are on for every non-UI product project (`Directory.Build.props`), and the
  Trimming/AOT/SingleFile analyzer categories fail the build, so reflection-based code is rejected at compile
  time rather than at publish time.
- Minimal APIs only (no MVC); endpoints compile to typed request delegates (`EnableRequestDelegateGenerator`);
  JSON uses source-generated `JsonSerializerContext`s; EF Core uses compiled models.
- Publish shape: API and worker publish JIT + ReadyToRun until EF Core supports NativeAOT, then flip the flag
  with no code change; the simulator and the CLI publish NativeAOT from day one (`PublishAot`); the CLI's
  `migrate` subcommand, which needs EF, ships as a separate JIT executable; MAUI uses its platform defaults;
  the Blazor Server console is excluded. `Wolfgang.Wms.Infrastructure` (EF Core: reflection-based, not
  trim/AOT-clean) has the analyzers off and is rooted (not trimmed) at publish.
- One assembly per project, single-file publish for distribution; self-contained runtime for the Windows
  installer and the CLI, framework-dependent inside containers (runtime patched by rebuilding the base image).
  Generated code (EF migrations, compiled model, source generators) is `[ExcludeFromCodeCoverage]`.

## Dependencies (E1.8)

Prefer the BCL and Microsoft packages. Any other package needs a written reason in the PR and sits behind an
interface this repository owns. Chris-Wolfgang libraries are exempt. Deny list: MediatR, AutoMapper, Moq,
FluentAssertions, Hangfire, MassTransit. Tests use xUnit with built-in asserts; NSubstitute for infrastructure
boundaries; hand-written fakes for Domain interfaces.
