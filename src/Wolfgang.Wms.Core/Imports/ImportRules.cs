// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// The limits and the policy names of an import request (E16.6).
/// </summary>
public static class ImportRules
{
    /// <summary>Most rows one file may carry; larger loads are split by the caller.</summary>
    public const int MaxRows = 10_000;

    /// <summary>The <c>policy</c> query value for <see cref="ImportPolicy.AllOrNothing"/>.</summary>
    public const string AllOrNothing = "all_or_nothing";

    /// <summary>The <c>policy</c> query value for <see cref="ImportPolicy.AcceptValidRows"/>.</summary>
    public const string AcceptValidRows = "accept_valid_rows";

    /// <summary>The <c>policy</c> query value for <see cref="ImportPolicy.ValidateOnly"/>.</summary>
    public const string ValidateOnly = "validate_only";

    /// <summary>The <c>format</c> query value that returns the rows as CSV instead of the JSON result.</summary>
    public const string CsvFormat = "csv";



    /// <summary>
    /// The policy a query value names, or <paramref name="fallback"/> for none; null for a value that is not a policy.
    /// </summary>
    public static ImportPolicy? ParsePolicy(string? text, ImportPolicy fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        return text.Trim().Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant() switch
        {
            AllOrNothing or "allornothing" => ImportPolicy.AllOrNothing,
            AcceptValidRows or "acceptvalidrows" => ImportPolicy.AcceptValidRows,
            ValidateOnly or "validateonly" => ImportPolicy.ValidateOnly,
            _ => null,
        };
    }



    /// <summary>
    /// The query value of a policy.
    /// </summary>
    public static string Name(ImportPolicy policy)
    {
        return policy switch
        {
            ImportPolicy.AllOrNothing => AllOrNothing,
            ImportPolicy.AcceptValidRows => AcceptValidRows,
            _ => ValidateOnly,
        };
    }



    /// <summary>
    /// Whether a run writes under <paramref name="policy"/> given its row outcomes: never for
    /// <see cref="ImportPolicy.ValidateOnly"/>, only when no row failed for <see cref="ImportPolicy.AllOrNothing"/>,
    /// and when at least one row has something to write for <see cref="ImportPolicy.AcceptValidRows"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is null.</exception>
    public static bool ShouldWrite(ImportPolicy policy, IReadOnlyList<ImportRowResult> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return policy switch
        {
            ImportPolicy.ValidateOnly => false,
            ImportPolicy.AllOrNothing => rows.Count > 0 && rows.All(r => r.Outcome != ImportRowOutcome.Failed) && rows.Any(Writes),
            _ => rows.Any(Writes),
        };
    }



    /// <summary>
    /// Null when a file of <paramref name="rowCount"/> rows may be imported, else the reason.
    /// </summary>
    public static string? ValidateSize(int rowCount)
    {
        if (rowCount <= 0)
        {
            return "The file has no rows.";
        }

        return rowCount <= MaxRows ? null : $"The file has {rowCount} rows; at most {MaxRows} may be imported at once. Split it.";
    }



    private static bool Writes(ImportRowResult row)
    {
        return row.Outcome is ImportRowOutcome.Inserted or ImportRowOutcome.Updated or ImportRowOutcome.Deleted;
    }
}
