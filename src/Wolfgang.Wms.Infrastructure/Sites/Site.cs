// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Sites;

/// <summary>
/// A row of <c>layout.site</c> (E16.1): one warehouse. Audited (E6.4) and versioned (E5.1). The code is kept as
/// entered and compared through <see cref="CodeNormalized"/>, the way role names are (E10.2), so the unique index
/// behaves the same on both providers.
/// </summary>
public sealed class Site : IVersionedEntity
{
    /// <summary>The server-assigned identifier.</summary>
    public long Id { get; set; }

    /// <summary>The short unique code as entered.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The code in the form codes are compared in (<see cref="SiteRules.Normalize"/>).</summary>
    public string CodeNormalized { get; set; } = string.Empty;

    /// <summary>The name users see.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The site's time zone id.</summary>
    public string TimeZone { get; set; } = string.Empty;

    /// <summary>False once the site is retired.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>When the row was last written (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Who last wrote the row.</summary>
    public string UpdatedBy { get; set; } = string.Empty;

    /// <inheritdoc/>
    public long RowVersion { get; set; }



    /// <summary>
    /// The row as the API reports it.
    /// </summary>
    public SiteInfo ToInfo()
    {
        return new SiteInfo(Id, Code, Name, TimeZone, IsActive, UpdatedAt, UpdatedBy, RowVersion);
    }



    /// <summary>
    /// Copies a valid draft into the row.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
    public void Apply(SiteDraft draft, DateTimeOffset now, string updatedBy)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Code = draft.Code.Trim();
        CodeNormalized = SiteRules.Normalize(draft.Code);
        Name = draft.Name.Trim();
        TimeZone = (draft.TimeZone ?? string.Empty).Trim();   // the store filled the organisation's default and the rules refused a blank one
        IsActive = draft.IsActive;
        UpdatedAt = now;
        UpdatedBy = updatedBy;
    }
}
