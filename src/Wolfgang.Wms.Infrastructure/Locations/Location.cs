// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Locations;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Locations;

/// <summary>
/// A row of <c>layout.location</c> (E17.1): one bin in a zone of a site. Audited (E6.4) and versioned (E5.1).
/// The code is kept as entered and compared through <see cref="CodeNormalized"/> within the site; the barcode
/// is compared as entered.
/// </summary>
public sealed class Location : IVersionedEntity
{
    /// <summary>The server-assigned identifier.</summary>
    public long Id { get; set; }

    /// <summary>The site the bin belongs to.</summary>
    public long SiteId { get; set; }

    /// <summary>The zone the bin is in.</summary>
    public long ZoneId { get; set; }

    /// <summary>The bin code as entered.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The code in the form codes are compared in (<see cref="LocationRules.Normalize"/>).</summary>
    public string CodeNormalized { get; set; } = string.Empty;

    /// <summary>The label on the bin.</summary>
    public string Barcode { get; set; } = string.Empty;

    /// <summary>The sortable walk order.</summary>
    public string WalkSequence { get; set; } = string.Empty;

    /// <summary>False for a bin never picked from.</summary>
    public bool IsPickable { get; set; } = true;

    /// <summary>False once the bin is retired.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>When the row was last written (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Who last wrote the row.</summary>
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>The id of the row this one was copied from (E16.5); null when created outright. Audited with the create.</summary>
    public long? CopiedFromId { get; set; }

    /// <inheritdoc/>
    public long RowVersion { get; set; }



    /// <summary>
    /// The row as the API reports it.
    /// </summary>
    public LocationInfo ToInfo()
    {
        return new LocationInfo(Id, SiteId, ZoneId, Code, Barcode, WalkSequence, IsPickable, IsActive, UpdatedAt, UpdatedBy, RowVersion, CopiedFromId);
    }



    /// <summary>
    /// True when applying <paramref name="draft"/> would change nothing (E16.6: an unchanged import row).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
    public bool Matches(LocationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return string.Equals(Code, draft.Code.Trim(), StringComparison.Ordinal)
            && string.Equals(Barcode, draft.Barcode.Trim(), StringComparison.Ordinal)
            && ZoneId == draft.ZoneId
            && string.Equals(WalkSequence, draft.WalkSequence.Trim(), StringComparison.Ordinal)
            && IsPickable == draft.IsPickable
            && IsActive == draft.IsActive;
    }



    /// <summary>
    /// Copies a valid draft into the row.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
    public void Apply(LocationDraft draft, DateTimeOffset now, string updatedBy)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Code = draft.Code.Trim();
        CodeNormalized = LocationRules.Normalize(draft.Code);
        Barcode = draft.Barcode.Trim();
        ZoneId = draft.ZoneId;
        WalkSequence = draft.WalkSequence.Trim();
        IsPickable = draft.IsPickable;
        IsActive = draft.IsActive;
        UpdatedAt = now;
        UpdatedBy = updatedBy;
    }
}
