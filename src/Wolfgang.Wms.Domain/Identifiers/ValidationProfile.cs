// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;

namespace Wolfgang.Wms.Domain.Identifiers;

/// <summary>
/// The rules for one customer-supplied identifier field (E3.7): totes, zones, bins, ERP ids, lots, serials,
/// badges. Normalisation (case, trim) runs before the length and format checks. The default profile stores
/// exactly what was received, trimmed, case-sensitive, capped at the field's system maximum, with only
/// control characters rejected (E3.6). Profiles cascade organisation → site (→ SKU for lot/serial) in the
/// settings module; this record is the value they resolve to.
/// </summary>
/// <param name="Field">The field the profile applies to (<c>tote_barcode</c>, <c>erp_release_id</c>).</param>
/// <param name="SystemMaxLength">The hard cap of the field's column; <see cref="MaxLength"/> can only lower it.</param>
public sealed record ValidationProfile(string Field, int SystemMaxLength)
{
    private readonly int _minLength;
    private readonly int? _maxLength;



    /// <summary>
    /// The field name, required.
    /// </summary>
    public string Field { get; } = string.IsNullOrWhiteSpace(Field) ? throw new ArgumentException("A field name is required.", nameof(Field)) : Field;



    /// <summary>
    /// The column cap; positive.
    /// </summary>
    public int SystemMaxLength { get; } = SystemMaxLength > 0
        ? SystemMaxLength
        : throw new ArgumentOutOfRangeException(nameof(SystemMaxLength), SystemMaxLength, "The system maximum length must be positive.");



    /// <summary>
    /// Letter-case normalisation; default <see cref="CaseHandling.NoChange"/>.
    /// </summary>
    public CaseHandling Case { get; init; } = CaseHandling.NoChange;



    /// <summary>
    /// Minimum length after normalisation; default 0, never negative.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int MinLength
    {
        get => _minLength;
        init => _minLength = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The minimum length cannot be negative.");
    }



    /// <summary>
    /// Maximum length after normalisation; null means the system cap. Never above the system cap; at least 1.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int? MaxLength
    {
        get => _maxLength;
        init => _maxLength = value is null or > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum length must be positive.");
    }



    /// <summary>
    /// Whether a blank value is an error.
    /// </summary>
    public bool Required { get; init; }



    /// <summary>
    /// Trim leading spaces before checking; default true.
    /// </summary>
    public bool TrimLeading { get; init; } = true;



    /// <summary>
    /// Trim trailing spaces before checking; default true.
    /// </summary>
    public bool TrimTrailing { get; init; } = true;



    /// <summary>
    /// Optional mask or regular expression the normalised value must match.
    /// </summary>
    public IdentifierFormat? Format { get; init; }



    /// <summary>
    /// The field holds GS1 element strings (pallet SSCC labels, GS1-128 case labels): every non-blank value is
    /// parsed by <see cref="Gs1.TryParse"/> and fails with rule <c>gs1</c> when its structure is wrong (E3.6).
    /// A value that starts with a GS1 symbology identifier (<see cref="Gs1.HasGs1SymbologyIdentifier"/>) is
    /// parsed in every field, whatever this says. Length and <see cref="Format"/> still apply on top.
    /// </summary>
    public bool Gs1ElementString { get; init; }



    /// <summary>
    /// The maximum length in force: the lower of <see cref="MaxLength"/> and <see cref="SystemMaxLength"/>.
    /// </summary>
    public int EffectiveMaxLength => Math.Min(MaxLength ?? SystemMaxLength, SystemMaxLength);



    /// <summary>
    /// Why the settings contradict each other (a <see cref="MinLength"/> above <see cref="EffectiveMaxLength"/>,
    /// so no non-blank value could pass), or null when they are consistent. Checked across properties here rather
    /// than in the <c>init</c> accessors because a <c>with</c> expression sets them one at a time;
    /// <see cref="IdentifierValidator.Validate"/> refuses a profile that has one.
    /// </summary>
    public string? ConfigurationError => MinLength > EffectiveMaxLength
        ? string.Create(CultureInfo.InvariantCulture, $"{Field}: the minimum length {MinLength} is above the maximum length {EffectiveMaxLength}.")
        : null;
}
