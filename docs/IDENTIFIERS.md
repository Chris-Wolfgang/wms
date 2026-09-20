# Customer-supplied identifiers (E3.6–E3.9)

ERP release ids, tote and container barcodes, zone and bin codes, lots, serials and badges arrive from the
customer's systems and labels. The product accepts them as they are unless an administrator says otherwise,
and applies the same rules everywhere. The rules live in `Wolfgang.Wms.Domain.Identifiers` and are shared with
the handheld (E1.4).

## Defaults (E3.6)

- Stored exactly as received, trimmed of leading and trailing spaces, case-sensitive, capped at the field's
  column length. Only control characters are rejected. No character-set or case rule unless configured.
- Unicode throughout: `nvarchar` / UTF-8 `text` in the database, UTF-8 on the API and in JSON/XML files.
  Legacy encodings (EBCDIC, code pages) exist only in flat files and are converted at the file connector edge
  in both directions (E24.2); the domain never sees a non-Unicode value.
- GS1 element strings are validated structurally, by default and always (`Gs1`): known application
  identifiers, fixed and maximum lengths, digits-only values, GTIN/SSCC/GLN/GSIN/GSRN check digits, `YYMMDD`
  dates (day `00` = end of month). Both the human-readable form `(01)09501101530003(17)261231(10)ABC` and the
  scanned form with FNC1 group separators (and a leading symbology identifier such as `]C1`) are accepted.
  Per-SKU lot and serial formats layer on top.
- Internally generated identifiers are server-assigned `long` only; no GUID columns; cursors and idempotency
  keys are opaque strings (E82.3).

## Validation profiles (E3.7)

A `ValidationProfile` per identifier field: case handling (`MakeUpper` | `MakeLower` | `NoChange`, default
`NoChange`), minimum length, maximum length (never above the system cap), required, trim leading, trim
trailing, and an optional format. Normalisation (trim, case) runs before the length and format checks, so a
`MakeUpper` profile with format `AAA-999` accepts `abc-123` and stores `ABC-123`.

`IdentifierValidator.Validate(profile, raw)` returns the normalised value or the field, the failed rule
(`required`, `control_characters`, `min_length`, `max_length`, `format`) and the expectation in words; the
message is the same at intake, scan, console, CSV import and API. Profiles cascade organisation → site
(→ SKU for lot/serial) and are edited on the Validation settings page, exported with settings and audited
(E6/E7/E9 stories deliver the storage and the page).

## Mask and regex formats (E3.8)

| Mask token | Matches | Example |
|------------|---------|---------|
| `A` | one letter | `AAA-999` → `ABC-123` |
| `9` | one digit | `9(8)` → `12345678` |
| `X` | one letter or digit | `T-X(6)` → `T-A1B2C3` |
| `?` | any one character | `?(3)` |
| `(n)` | repeat the preceding token n times | `9(8)` |
| anything else | itself | `-`, `.`, `LOT ` |

Masks compile to an anchored .NET regular expression (`MaskCompiler`, shown in the UI), so one engine
validates both; a regex is written in .NET syntax and is always anchored to the whole value. Every format is
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
