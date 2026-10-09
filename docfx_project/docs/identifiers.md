# Your identifiers

Totes, containers, zones, bins, ERP release ids, lots, serials and badges are numbered by you, not by the WMS.
This page explains how the WMS treats those identifiers, what an administrator can tighten, and what a
rejection message means. The same rules apply everywhere a value enters: receiving, scanning, the console,
CSV import and the API.

## What happens by default

Out of the box the WMS accepts your identifiers as they are:

- **Stored exactly as received**, with leading and trailing spaces removed. `Tote-0017` and `TOTE-0017` are
  different identifiers; nothing is upper-cased unless you ask for it.
- **Any characters you use**, in any language: `Zürich-Ω-日本` is a valid bin code.
- **Only control characters are refused**: tabs, line breaks and other invisible codes that cannot be printed
  on a label. A value containing one is rejected with `control characters are not allowed`.
- **Up to the field's maximum length.** Each field has a hard upper limit set by the system; you can lower it
  but not raise it.

## GS1 barcodes

GS1 element strings (pallet SSCC labels, GS1-128 case labels, GS1 DataMatrix codes) carry several values, each
behind an application identifier (AI) such as `(01)` for the GTIN or `(17)` for the expiry date. The WMS
always checks their structure. It reads both the printed form and the scanned form:

| Form | Example |
|------|---------|
| Printed (human-readable) | `(01)09501101530003(17)261231(10)ABC` |
| Scanned | `]C1` + `0109501101530003` + `17261231` + `10ABC`, with an FNC1 separator after a variable-length value |

A GS1 value is checked when the field is set up to hold GS1 element strings, and also in any field whenever
the scanner reports a GS1 barcode type (GS1-128, GS1 DataBar, GS1 DataMatrix, GS1 QR Code or GS1 DotCode).
A plain-text field that is not set up for GS1 stores a typed value such as `(01)123` as ordinary text.

The checks are:

- the AI is one the WMS knows; an unknown AI is an error, never silently accepted;
- the value has the length GS1 defines for that AI, for example exactly 14 digits for a GTIN and 1 to 20
  characters for a batch or lot;
- digits only where GS1 requires digits;
- a correct check digit on GTIN, SSCC, GLN, GSIN and GSRN values;
- dates in `YYMMDD` form (day `00` means the last day of the month);
- the expiry date and time `(7003)` as `YYMMDDhhmm`, a real date with an hour from `00` to `23` and a minute
  from `00` to `59`;
- the ship-to postal code with country `(421)` starts with a three-digit ISO 3166 country code, such as `840`
  for the United States or `276` for Germany, followed by the postal code;
- no tabs, line breaks or other control characters inside a value. Spaces inside a value are kept exactly
  as they were encoded.

A rejected GS1 value names the AI and the rule, for example:

```
tote_barcode: (01) GTIN: check digit is wrong.
tote_barcode: (17) Expiry date: not a YYMMDD date.
tote_barcode: (421) Ship-to postal code with country: expected a 3-digit ISO 3166 country code and 1 to 9 more characters.
```

Lot and serial formats you set per item (below) are checked in addition to the GS1 rules, never instead of
them.

## Tightening the rules for a field

An administrator can set these options for each identifier field. Settings made for the organisation apply to
every site unless a site overrides them; lot and serial settings can also be set per item.

| Option | What it does | Default |
|--------|--------------|---------|
| Case | Make the value upper case, lower case, or leave it unchanged | Unchanged |
| Minimum length | Fewest characters allowed | 0 |
| Maximum length | Most characters allowed; never above the field's system limit | The system limit |
| Required | Whether the value may be blank | Not required |
| Trim leading spaces | Remove spaces before the value | On |
| Trim trailing spaces | Remove spaces after the value | On |
| Format | A mask or a regular expression the value must match | None |

Trimming and case changes happen first, then the checks. So a field set to upper case with the format
`AAA-999` accepts `abc-123` and stores `ABC-123`.

A GS1 element string is the exception: it is checked and stored exactly as encoded, with no trimming or
case change, because spaces and letter case are part of its values.

The minimum length cannot be negative, the maximum length must be at least 1, and the minimum cannot be
larger than the maximum: a field set up that way could never accept a value, so the WMS refuses the setup
instead.

## Formats: masks

A mask is the simplest way to describe a format. Each character stands for one character of the value:

| Mask character | Matches | Example mask | Accepts | Rejects |
|----------------|---------|--------------|---------|---------|
| `A` | one letter | `AAA-999` | `ABC-123` | `AB-123` |
| `9` | one digit | `9(8)` | `12345678` | `1234567` |
| `X` | one letter or digit | `T-X(6)` | `T-A1B2C3` | `T-A1B2C` |
| `?` | any one character | `?(3)` | `a b`, `#1!` | `ab` |
| `(n)` | the character before it, n times | `9(8)` | eight digits | seven digits |
| anything else | itself | `LOT 9` | `LOT 7` | `LOT-7` |

The mask always describes the whole value: `9(8)` accepts eight digits and nothing more, not even a line
break after them. Letters in a mask match either case (`AAA` accepts `abc`); use the Case option to store them
one way.

## Formats: regular expressions

For formats a mask cannot express, a field can use a .NET regular expression instead, for example
`[A-Z]{2}\d+` (two capital letters followed by digits). The expression always applies to the whole value,
whether or not you write `^` and `$`: `ab|cd` accepts `ab` or `cd` but not `abcd`, and no value is accepted
because of an extra line break at its end.

Every format is checked in a way that cannot stall receiving or a scan. Most expressions run on an engine
whose time grows only with the length of the value. Expressions that need backreferences or lookarounds run
with a 100 millisecond limit per value, and a value that hits the limit counts as not matching.

## What a rejection tells you

Every rejection names the field and what was expected, in the same words on every screen and in the API:

| Rule | Example message |
|------|-----------------|
| Required | `tote_barcode: a value is required` |
| GS1 structure | `tote_barcode: (01) GTIN: check digit is wrong.` |
| Control characters | `tote_barcode: control characters are not allowed` |
| Minimum length | `tote_barcode: at least 3 characters` |
| Maximum length | `tote_barcode: at most 5 characters` |
| Format | `tote_barcode: format T-9(4)` |

## Coming next

The Validation settings page, where these options are edited, and a format tester that shows the compiled
expression and which of your existing values would pass or fail, arrive in later releases.
