# Customer-supplied identifiers (E3.6–E3.9)

ERP release ids, tote and container barcodes, zone and bin codes, lots, serials and badges arrive from the
customer's systems and labels. The product accepts them as they are unless an administrator says otherwise,
and applies the same rules everywhere. The rules live in `Wolfgang.Wms.Domain.Identifiers` and are shared with
the handheld (E1.4).

This page is the engineering view. The operator- and customer-facing page is
[`docfx_project/docs/identifiers.md`](../docfx_project/docs/identifiers.md), published with the docs site; keep
the two in step when a rule changes.

## Defaults (E3.6)

- Stored exactly as received, trimmed of leading and trailing spaces, case-sensitive, capped at the field's
  column length. Only control characters are rejected. No character-set or case rule unless configured.
- Unicode throughout: `nvarchar` / UTF-8 `text` in the database, UTF-8 on the API and in JSON/XML files.
  Legacy encodings (EBCDIC, code pages) exist only in flat files and are converted at the file connector edge
  in both directions (E24.2); the domain never sees a non-Unicode value.
- GS1 element strings are validated structurally, by default and always (`Gs1`): known application
  identifiers, fixed and maximum lengths, digits-only values, GTIN/SSCC/GLN/GSIN/GSRN check digits, `YYMMDD`
  dates (day `00` = end of month), `YYMMDDhhmm` for AI 7003 (real date, hour 00–23, minute 00–59), and an
  ISO 3166 numeric country code before the postal code in AI 421. Lengths follow the GS1 Barcode Syntax
  Dictionary (7001 is N13 fixed, 7004 is N..4 variable). Both the human-readable form
  `(01)09501101530003(17)261231(10)ABC` and the scanned form with FNC1 group separators (and a leading
  symbology identifier such as `]C1`) are accepted. The element string is not trimmed: values keep their
  spaces exactly as encoded, and a tab or line break anywhere is a control-character error. Per-SKU lot and
  serial formats layer on top.
- `IdentifierValidator` routes a value through `Gs1.TryParse` when the profile says the field holds GS1
  element strings (`ValidationProfile.Gs1ElementString`) and, in every field, when the value starts with a GS1
  symbology identifier (`]C1`, `]e0`, `]d2`, `]Q3`, `]J1`). A structural error fails with rule `gs1` and the
  parser's message (for example `(01) GTIN: check digit is wrong.`). In a GS1 value the FNC1 separators are
  structure, not control characters; length and format checks still apply on top.
- Internally generated identifiers are server-assigned `long` only; no GUID columns; cursors and idempotency
  keys are opaque strings (E82.3).

## Validation profiles (E3.7)

A `ValidationProfile` per identifier field: case handling (`MakeUpper` | `MakeLower` | `NoChange`, default
`NoChange`), minimum length, maximum length (never above the system cap), required, trim leading, trim
trailing, and an optional format. Normalisation (trim, case) runs before the length and format checks, so a
`MakeUpper` profile with format `AAA-999` accepts `abc-123` and stores `ABC-123`.

A minimum length below 0 or a maximum length below 1 throws as it is set (also through a `with` expression).
A minimum above the effective maximum cannot be caught that way, because `with` sets properties one at a time;
`ValidationProfile.ConfigurationError` names it, and `IdentifierValidator.Validate` refuses such a profile with
an `ArgumentException` rather than rejecting every value.

`IdentifierValidator.Validate(profile, raw)` returns the normalised value or the field, the failed rule
(`required`, `gs1`, `control_characters`, `min_length`, `max_length`, `format`) and the expectation in words; the
message is the same at intake, scan, console, CSV import and API. Profiles cascade organisation → site
(→ SKU for lot/serial) and are edited on the Validation settings page, exported with settings and audited
(E6/E7/E9 stories deliver the storage and the page).

## Mask and regex formats (E3.8)

| Mask token | Matches | Example |
|------------|---------|---------|
| `A` | one letter | `AAA-999` → `ABC-123` |
| `9` | one digit | `9(8)` → `12345678` |
| `X` | one letter or digit | `T-X(6)` → `T-A1B2C3` |
| `?` | any one character, including a line feed (`[\s\S]`) | `?(3)` |
| `(n)` | repeat the preceding token n times | `9(8)` |
| anything else | itself | `-`, `.`, `LOT ` |

Masks compile to an anchored .NET regular expression (`MaskCompiler`, shown in the UI), so one engine
validates both; a regex is written in .NET syntax and is always anchored to the whole value with `\A(?:…)\z`
(masks compile to `\A…\z` too). `^`/`$` are not used for this because .NET's `$` also matches before a final
line feed, so `AB` would accept `AB\n`. Every format is
evaluated with `RegexOptions.NonBacktracking` where the pattern allows it (linear time, no catastrophic
backtracking) and with a 100 ms match timeout on the backtracking engine otherwise (backreferences,
lookarounds), so a bad pattern cannot stall intake or a scan; a timeout counts as no match.

## Format tester (E3.9)

`POST /api/v0/validation/test` takes a format (mask or regex) and any number of values and returns the
compiled regular expression and pass/fail per value, so the Validation settings page, the CLI and customer
tools share one tester. The page adds live results as the user types, "load recent values" from the field's
last N real values, and "Run regression" over every existing value (server-side, streamed, cancellable;
counts always shown next to percentages, percentages truncated, never rounded up; failing sample of 50
downloadable as CSV; a save with failures asks for confirmation and stores the regression result in the
audit record). Those parts arrive with the settings storage and the Validation page.
