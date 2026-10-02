// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.UnitTests.Settings;

/// <summary>
/// E6.1/E7: the two cascade chains, stored names and scope references.
/// </summary>
public sealed class SettingScopeTests
{
    [Theory]
    [InlineData(SettingScope.Organization, null, "organization", SettingScopes.Organization)]
    [InlineData(SettingScope.Site, SettingScope.Organization, "site", SettingScopes.Site)]
    [InlineData(SettingScope.Zone, SettingScope.Site, "zone", SettingScopes.Zone)]
    [InlineData(SettingScope.Sku, SettingScope.Site, "sku", SettingScopes.Sku)]
    public void Scopes_have_a_parent_a_stored_name_and_a_flag(SettingScope scope, SettingScope? parent, string storedName, SettingScopes flag)
    {
        ArgumentNullException.ThrowIfNull(storedName);

        Assert.Equal(parent, scope.Parent());
        Assert.Equal(storedName, scope.StoredName());
        Assert.Equal(flag, scope.AsFlag());
        Assert.True(SettingScopeExtensions.TryParseScope(storedName.ToUpperInvariant(), out var parsed));
        Assert.Equal(scope, parsed);
    }



    [Fact]
    public void Unknown_scopes_are_rejected()
    {
        const SettingScope bogus = (SettingScope)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => bogus.Parent());
        Assert.Throws<ArgumentOutOfRangeException>(() => bogus.StoredName());
        Assert.Throws<ArgumentOutOfRangeException>(() => bogus.AsFlag());
        Assert.False(SettingScopeExtensions.TryParseScope("aisle", out _));
        Assert.False(SettingScopeExtensions.TryParseScope(null, out _));
    }



    [Fact]
    public void Chains_allow_their_members_only()
    {
        Assert.True(SettingScopes.OrganizationToSku.Allows(SettingScope.Sku));
        Assert.False(SettingScopes.OrganizationToSku.Allows(SettingScope.Zone));
        Assert.True(SettingScopes.OrganizationToSite.Allows(SettingScope.Site));
        Assert.False(SettingScopes.OrganizationToSite.Allows(SettingScope.Zone));
        Assert.False(SettingScopes.None.Allows(SettingScope.Organization));
    }



    [Fact]
    public void Scope_references_name_the_organisation_alone_and_others_with_their_id()
    {
        Assert.Equal(new SettingScopeRef(SettingScope.Organization, 0), SettingScopeRef.Organization);
        Assert.Equal("organization", SettingScopeRef.Organization.ToString());
        Assert.Equal("site:7", SettingScopeRef.Site(7).ToString());
        Assert.Equal("zone:12", SettingScopeRef.Zone(12).ToString());
        Assert.Equal("sku:3", SettingScopeRef.Sku(3).ToString());
        Assert.Equal(SettingScope.Sku, SettingScopeRef.Sku(3).Type);
    }
}
