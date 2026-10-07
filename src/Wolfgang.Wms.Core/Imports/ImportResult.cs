// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// What an import run produced (E16.6): the counts, whether anything was written, and one line per row. The
/// same shape is returned by the API, shown in the console and downloadable as CSV (<see cref="ToCsv"/>).
/// </summary>
/// <param name="Entity">The entity type the file carried (<c>zones</c>, <c>locations</c>).</param>
/// <param name="Policy">The bad-row policy the run used.</param>
/// <param name="Written">True when the rows were written; false under <see cref="ImportPolicy.ValidateOnly"/> and for a rolled-back <see cref="ImportPolicy.AllOrNothing"/> run.</param>
/// <param name="Inserted">Rows with a new key.</param>
/// <param name="Updated">Rows whose key existed and changed.</param>
/// <param name="Deleted">Rows retired.</param>
/// <param name="Unchanged">Rows that needed nothing.</param>
/// <param name="Failed">Rows refused.</param>
/// <param name="Rows">One entry per row, in file order.</param>
public sealed record ImportResult
(
    string Entity,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ImportPolicy>))] ImportPolicy Policy,
    bool Written,
    int Inserted,
    int Updated,
    int Deleted,
    int Unchanged,
    int Failed,
    IReadOnlyList<ImportRowResult> Rows
)
{
    /// <summary>The CSV header of <see cref="ToCsv"/>.</summary>
    public const string CsvHeader = "row,key,outcome,code,message";



    /// <summary>The rows, never null.</summary>
    public IReadOnlyList<ImportRowResult> Rows { get; } = Rows ?? throw new ArgumentNullException(nameof(Rows));



    /// <summary>
    /// Builds the result from its rows, counting the outcomes.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static ImportResult From(string entity, ImportPolicy policy, bool written, IReadOnlyList<ImportRowResult> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);
        ArgumentNullException.ThrowIfNull(rows);

        return new ImportResult
        (
            entity,
            policy,
            written,
            rows.Count(r => r.Outcome == ImportRowOutcome.Inserted),
            rows.Count(r => r.Outcome == ImportRowOutcome.Updated),
            rows.Count(r => r.Outcome == ImportRowOutcome.Deleted),
            rows.Count(r => r.Outcome == ImportRowOutcome.Unchanged),
            rows.Count(r => r.Outcome == ImportRowOutcome.Failed),
            rows
        );
    }



    /// <summary>
    /// The rows as CSV (RFC 4180: comma-separated, quoted where needed, CRLF line ends) with the header
    /// <see cref="CsvHeader"/>; what the console offers for download.
    /// </summary>
    public string ToCsv()
    {
        var text = new StringBuilder(CsvHeader).Append("\r\n");
        foreach (var row in Rows)
        {
            text.Append(row.Row.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Quote(row.Key)).Append(',')
                .Append(row.Outcome.ToString()).Append(',')
                .Append(Quote(row.Code)).Append(',')
                .Append(Quote(row.Message)).Append("\r\n");
        }

        return text.ToString();
    }



    private static string Quote(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.AsSpan().IndexOfAny(",\"\r\n") < 0 ? value : "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
