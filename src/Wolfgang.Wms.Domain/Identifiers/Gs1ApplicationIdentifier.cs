// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;

namespace Wolfgang.Wms.Domain.Identifiers;

/// <summary>
/// One GS1 application identifier (AI) and the structural rule for its value (E3.6): fixed or maximum
/// length, digits only or not, date format, check digit. The table covers the AIs a warehouse meets on
/// pallet, case and item labels; an unknown AI is a structural error, never silently accepted.
/// </summary>
/// <param name="Code">The AI digits (<c>01</c>, <c>17</c>, <c>3103</c>).</param>
/// <param name="Description">Short name (<c>GTIN</c>, <c>Expiry date</c>).</param>
/// <param name="FixedLength">Exact value length, or null when variable.</param>
/// <param name="MaxLength">Maximum value length (equal to <paramref name="FixedLength"/> when fixed).</param>
/// <param name="Numeric">True when the value is digits only.</param>
/// <param name="Rule">Extra structural rule beyond length and character class.</param>
public sealed record Gs1ApplicationIdentifier(string Code, string Description, int? FixedLength, int MaxLength, bool Numeric, Gs1ValueRule Rule = Gs1ValueRule.None)
{
    private static readonly Dictionary<string, Gs1ApplicationIdentifier> Table = Build();



    /// <summary>
    /// True when the value length is known from the AI alone (no separator needed after it).
    /// </summary>
    public bool IsFixedLength => FixedLength is not null;



    /// <summary>
    /// The AI whose code starts <paramref name="text"/> (2-, 3- or 4-digit codes, longest known first), or null.
    /// </summary>
    public static Gs1ApplicationIdentifier? Find(ReadOnlySpan<char> text)
    {
        for (var length = 4; length >= 2; length--)
        {
            if (text.Length >= length && Table.TryGetValue(text[..length].ToString(), out var ai))
            {
                return ai;
            }
        }

        return null;
    }



    /// <summary>
    /// Every AI in the table, by code.
    /// </summary>
    public static IReadOnlyCollection<Gs1ApplicationIdentifier> Known => Table.Values;



    private static Dictionary<string, Gs1ApplicationIdentifier> Build()
    {
        var table = new Dictionary<string, Gs1ApplicationIdentifier>(StringComparer.Ordinal);
        AddIdentification(table);
        AddDatesAndCounts(table);
        AddMeasuresAndLogistics(table);
        return table;
    }



    private static void AddIdentification(Dictionary<string, Gs1ApplicationIdentifier> table)
    {
        Fixed(table, "00", "SSCC", 18, Gs1ValueRule.CheckDigit);
        Fixed(table, "01", "GTIN", 14, Gs1ValueRule.CheckDigit);
        Fixed(table, "02", "GTIN of contained items", 14, Gs1ValueRule.CheckDigit);
        Variable(table, "10", "Batch or lot", 20);
        Fixed(table, "20", "Internal product variant", 2);
        Variable(table, "21", "Serial number", 20);
        Variable(table, "22", "Consumer product variant", 20);
        Variable(table, "240", "Additional product identification", 30);
        Variable(table, "241", "Customer part number", 30);
        Variable(table, "242", "Made-to-order variation", 6, numeric: true);
        Variable(table, "250", "Secondary serial number", 30);
        Variable(table, "251", "Reference to source entity", 30);
        Variable(table, "400", "Customer purchase order", 30);
        Variable(table, "401", "GINC", 30);
        Fixed(table, "402", "GSIN", 17, Gs1ValueRule.CheckDigit);
        Variable(table, "403", "Routing code", 30);
        for (var i = 410; i <= 417; i++)
        {
            Fixed(table, i.ToString(CultureInfo.InvariantCulture), "GLN", 13, Gs1ValueRule.CheckDigit);
        }

        Variable(table, "420", "Ship-to postal code", 20);
        Variable(table, "421", "Ship-to postal code with country", 12);
        Variable(table, "90", "Trading partner agreed information", 30);
        for (var i = 91; i <= 99; i++)
        {
            Variable(table, i.ToString(CultureInfo.InvariantCulture), "Company internal", 90);
        }
    }



    private static void AddDatesAndCounts(Dictionary<string, Gs1ApplicationIdentifier> table)
    {
        Fixed(table, "11", "Production date", 6, Gs1ValueRule.Date);
        Fixed(table, "12", "Due date", 6, Gs1ValueRule.Date);
        Fixed(table, "13", "Packaging date", 6, Gs1ValueRule.Date);
        Fixed(table, "15", "Best before date", 6, Gs1ValueRule.Date);
        Fixed(table, "16", "Sell by date", 6, Gs1ValueRule.Date);
        Fixed(table, "17", "Expiry date", 6, Gs1ValueRule.Date);
        Variable(table, "30", "Variable count", 8, numeric: true);
        Variable(table, "37", "Count of trade items", 8, numeric: true);
        Fixed(table, "7003", "Expiry date and time", 10);
        Fixed(table, "7006", "First freeze date", 6, Gs1ValueRule.Date);
    }



    private static void AddMeasuresAndLogistics(Dictionary<string, Gs1ApplicationIdentifier> table)
    {
        for (var prefix = 310; prefix <= 369; prefix++)
        {
            for (var decimals = 0; decimals <= 5; decimals++)
            {
                Fixed(table, prefix.ToString(CultureInfo.InvariantCulture) + decimals.ToString(CultureInfo.InvariantCulture), "Measure", 6);
            }
        }

        Variable(table, "7001", "NATO stock number", 13, numeric: true);
        Variable(table, "7002", "Meat cut", 30);
        Variable(table, "7004", "Active potency", 4, numeric: true);
        Variable(table, "7005", "Catch area", 12);
        Variable(table, "8003", "GRAI", 30);
        Variable(table, "8004", "GIAI", 30);
        Fixed(table, "8005", "Price per unit", 6);
        Fixed(table, "8006", "ITIP", 18);
        Fixed(table, "8017", "GSRN provider", 18, Gs1ValueRule.CheckDigit);
        Fixed(table, "8018", "GSRN recipient", 18, Gs1ValueRule.CheckDigit);
        Variable(table, "8020", "Payment slip reference", 25);
        Variable(table, "8200", "Extended packaging URL", 70);
    }



    private static void Fixed(Dictionary<string, Gs1ApplicationIdentifier> table, string code, string description, int length, Gs1ValueRule rule = Gs1ValueRule.None)
    {
        table[code] = new Gs1ApplicationIdentifier(Code: code, Description: description, FixedLength: length, MaxLength: length, Numeric: true, Rule: rule);
    }



    private static void Variable(Dictionary<string, Gs1ApplicationIdentifier> table, string code, string description, int max, bool numeric = false)
    {
        table[code] = new Gs1ApplicationIdentifier(Code: code, Description: description, FixedLength: null, MaxLength: max, Numeric: numeric);
    }
}
