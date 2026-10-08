// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Zones;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Zones;

/// <summary>
/// A row of <c>layout.zone</c> (E16.2): a picking area, bulk storage or resolution zone within a site. Audited
/// (E6.4) and versioned (E5.1). The code is kept as entered and compared through <see cref="CodeNormalized"/>
/// within the site. The resolution-zone properties are flattened into nullable columns and the assigned
/// resolvers into <c>layout.zone_resolver</c>.
/// </summary>
public sealed class Zone : IVersionedEntity
{
    /// <summary>The server-assigned identifier.</summary>
    public long Id { get; set; }

    /// <summary>The site the zone belongs to.</summary>
    public long SiteId { get; set; }

    /// <summary>The code as entered.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The code in the form codes are compared in (<see cref="ZoneRules.Normalize"/>).</summary>
    public string CodeNormalized { get; set; } = string.Empty;

    /// <summary>The name users see.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>What the zone is for.</summary>
    public ZoneType Type { get; set; }

    /// <summary>The sortable prefix of the zone's locations' walk sequences; null for none.</summary>
    public string? WalkOrderPrefix { get; set; }

    /// <summary>True when the pick zone is also the conveyor's error/overflow lane.</summary>
    public bool IsRejectLane { get; set; }

    /// <summary>Resolution zones: the bin resolved stock is restocked to.</summary>
    public string? RestockingBin { get; set; }

    /// <summary>Resolution zones: the container returns are collected in.</summary>
    public string? ReturnsContainer { get; set; }

    /// <summary>Resolution zones: whether weight failures are routed here.</summary>
    public bool AcceptsWeightFailures { get; set; }

    /// <summary>Resolution zones: whether shorts are resolved here.</summary>
    public bool AcceptsShorts { get; set; }

    /// <summary>Resolution zones: whether adjustments are resolved here.</summary>
    public bool AcceptsAdjustments { get; set; }

    /// <summary>Resolution zones: whether misdirects are resolved here.</summary>
    public bool AcceptsMisdirects { get; set; }

    /// <summary>Resolution zones: true for a virtual queue, false for a physical lane.</summary>
    public bool IsVirtualQueue { get; set; }

    /// <summary>False once the zone is retired.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>When the row was last written (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Who last wrote the row.</summary>
    public string UpdatedBy { get; set; } = string.Empty;

    /// <inheritdoc/>
    public long RowVersion { get; set; }

    /// <summary>Resolution zones: the users assigned to resolve here.</summary>
    public List<ZoneResolver> Resolvers { get; } = [];



    /// <summary>
    /// The row as the API reports it.
    /// </summary>
    public ZoneInfo ToInfo()
    {
        var resolution = Type == ZoneType.Resolution
            ? new ResolutionZone(RestockingBin, ReturnsContainer, Resolvers.Select(r => r.UserId).Order().ToList(), AcceptsWeightFailures, AcceptsShorts, AcceptsAdjustments, AcceptsMisdirects, IsVirtualQueue)
            : null;
        return new ZoneInfo(Id, SiteId, Code, Name, Type, WalkOrderPrefix, IsRejectLane, resolution, IsActive, UpdatedAt, UpdatedBy, RowVersion);
    }



    /// <summary>
    /// Copies a valid draft into the row, replacing the assigned resolvers.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
    public void Apply(ZoneDraft draft, DateTimeOffset now, string updatedBy)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Code = draft.Code.Trim();
        CodeNormalized = ZoneRules.Normalize(draft.Code);
        Name = draft.Name.Trim();
        Type = draft.Type;
        WalkOrderPrefix = Trimmed(draft.WalkOrderPrefix);
        IsRejectLane = draft.IsRejectLane;
        ApplyResolution(draft.Resolution);
        IsActive = draft.IsActive;
        UpdatedAt = now;
        UpdatedBy = updatedBy;
    }



    private void ApplyResolution(ResolutionZone? resolution)
    {
        RestockingBin = Trimmed(resolution?.RestockingBin);
        ReturnsContainer = Trimmed(resolution?.ReturnsContainer);
        AcceptsWeightFailures = resolution?.AcceptsWeightFailures ?? false;
        AcceptsShorts = resolution?.AcceptsShorts ?? false;
        AcceptsAdjustments = resolution?.AcceptsAdjustments ?? false;
        AcceptsMisdirects = resolution?.AcceptsMisdirects ?? false;
        IsVirtualQueue = resolution?.IsVirtualQueue ?? false;

        var wanted = resolution?.ResolverUserIds ?? [];
        Resolvers.RemoveAll(r => !wanted.Contains(r.UserId));
        foreach (var userId in wanted.Where(id => Resolvers.TrueForAll(r => r.UserId != id)))
        {
            Resolvers.Add(new ZoneResolver { UserId = userId });
        }
    }



    private static string? Trimmed(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
