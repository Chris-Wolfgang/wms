// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

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
    /// Minimum length after normalisation; default 0.
    /// </summary>
    public int MinLength { get; init; }



    /// <summary>
    /// Maximum length after normalisation; null means the system cap. Never above the system cap.
    /// </summary>
    public int? MaxLength { get; init; }



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
    /// The maximum length in force: the lower of <see cref="MaxLength"/> and <see cref="SystemMaxLength"/>.
    /// </summary>
    public int EffectiveMaxLength => Math.Min(MaxLength ?? SystemMaxLength, SystemMaxLength);
}
