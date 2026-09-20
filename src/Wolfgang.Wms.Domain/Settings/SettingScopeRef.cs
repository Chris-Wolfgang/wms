// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Settings;

/// <summary>
/// One concrete scope a setting value belongs to: a scope type and the id of the site, zone or SKU (E6.2's
/// <c>scope_type</c>/<c>scope_id</c>). The organisation is the single scope with id 0.
/// </summary>
/// <param name="Type">The scope type.</param>
/// <param name="Id">The site, zone or SKU id; 0 for the organisation.</param>
public readonly record struct SettingScopeRef(SettingScope Type, long Id)
{
    /// <summary>
    /// The one organisation scope.
    /// </summary>
    public static SettingScopeRef Organization { get; } = new(SettingScope.Organization, 0);



    /// <summary>
    /// A site scope.
    /// </summary>
    public static SettingScopeRef Site(long id)
    {
        return new SettingScopeRef(SettingScope.Site, id);
    }



    /// <summary>
    /// A zone scope.
    /// </summary>
    public static SettingScopeRef Zone(long id)
    {
        return new SettingScopeRef(SettingScope.Zone, id);
    }



    /// <summary>
    /// A SKU scope.
    /// </summary>
    public static SettingScopeRef Sku(long id)
    {
        return new SettingScopeRef(SettingScope.Sku, id);
    }



    /// <inheritdoc/>
    public override string ToString()
    {
        return Type == SettingScope.Organization ? Type.StoredName() : Type.StoredName() + ":" + Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
