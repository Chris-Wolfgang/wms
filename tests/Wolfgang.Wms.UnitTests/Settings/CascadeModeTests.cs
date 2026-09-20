// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.UnitTests.Settings;

/// <summary>
/// E7.2: stored names, what each mode delegates to, which modes a key allows at a scope, and the chain
/// order the delegation rules rely on.
/// </summary>
public sealed class CascadeModeTests
{
    [Theory]
    [InlineData(CascadeMode.Value, "value", null)]
    [InlineData(CascadeMode.PerSite, "per_site", SettingScope.Site)]
    [InlineData(CascadeMode.PerZone, "per_zone", SettingScope.Zone)]
    [InlineData(CascadeMode.PerSku, "per_sku", SettingScope.Sku)]
    public void Modes_have_a_stored_name_and_a_target(CascadeMode mode, string name, SettingScope? target)
    {
        ArgumentNullException.ThrowIfNull(name);

        Assert.Equal(name, mode.StoredName());
        Assert.Equal(target, mode.DelegatesTo());
        Assert.True(CascadeModeExtensions.TryParseMode(name.ToUpperInvariant(), out var parsed));
        Assert.Equal(mode, parsed);
    }



    [Fact]
    public void Null_and_empty_parse_as_value_and_unknown_names_are_rejected()
    {
        Assert.True(CascadeModeExtensions.TryParseMode(null, out var fromNull));
        Assert.True(CascadeModeExtensions.TryParseMode(" ", out var fromBlank));
        Assert.Equal(CascadeMode.Value, fromNull);
        Assert.Equal(CascadeMode.Value, fromBlank);
        Assert.False(CascadeModeExtensions.TryParseMode("per_aisle", out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => ((CascadeMode)9).StoredName());
        Assert.Throws<ArgumentOutOfRangeException>(() => ((CascadeMode)9).DelegatesTo());
    }



    [Theory]
    [InlineData(SettingScope.Organization, SettingScopes.OrganizationToZone, new[] { CascadeMode.Value, CascadeMode.PerSite, CascadeMode.PerZone })]
    [InlineData(SettingScope.Organization, SettingScopes.OrganizationToSku, new[] { CascadeMode.Value, CascadeMode.PerSite, CascadeMode.PerSku })]
    [InlineData(SettingScope.Organization, SettingScopes.Organization, new[] { CascadeMode.Value })]
    [InlineData(SettingScope.Site, SettingScopes.OrganizationToZone, new[] { CascadeMode.Value, CascadeMode.PerZone })]
    [InlineData(SettingScope.Site, SettingScopes.OrganizationToSku, new[] { CascadeMode.Value, CascadeMode.PerSku })]
    [InlineData(SettingScope.Zone, SettingScopes.OrganizationToZone, new[] { CascadeMode.Value })]
    [InlineData(SettingScope.Sku, SettingScopes.OrganizationToSku, new[] { CascadeMode.Value })]
    public void Allowed_modes_delegate_only_to_scopes_below_that_the_key_allows(SettingScope scope, SettingScopes keyScopes, CascadeMode[] expected)
    {
        Assert.Equal(expected, CascadeModeExtensions.AllowedAt(scope, keyScopes));
    }



    [Fact]
    public void Below_follows_the_parent_chain()
    {
        Assert.True(CascadeModeExtensions.IsBelow(SettingScope.Zone, SettingScope.Organization));
        Assert.True(CascadeModeExtensions.IsBelow(SettingScope.Sku, SettingScope.Site));
        Assert.False(CascadeModeExtensions.IsBelow(SettingScope.Site, SettingScope.Zone));
        Assert.False(CascadeModeExtensions.IsBelow(SettingScope.Zone, SettingScope.Sku));
        Assert.False(CascadeModeExtensions.IsBelow(SettingScope.Organization, SettingScope.Organization));
    }
}
