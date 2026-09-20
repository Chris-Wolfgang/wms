// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.UnitTests.Settings;

/// <summary>
/// E6.1: the registry is the union of the modules' declared settings, ordered by name, duplicates refused;
/// a write is checked against registration, scope and value; descriptors mask secrets.
/// </summary>
public sealed class SettingRegistryTests
{
    private static readonly SettingKey<int> MaxTotes = new("picking.max_totes", 1, "Totes a picker may carry.") { Validator = v => v > 0 ? null : "must be positive" };
    private static readonly SettingKey<bool> Bulk = new("feature.bulk_picking", false, "Bulk picking.") { Scopes = SettingScopes.OrganizationToSite };
    private static readonly SettingKey<SecretText> Password = new("smtp.password", new SecretText("s3cret"), "SMTP password.") { Scopes = SettingScopes.Organization };



    [Fact]
    public void Registry_is_built_from_every_registered_modules_settings_ordered_by_name()
    {
        var services = new ServiceCollection();
        services.AddWmsSettingsModule();
        services.AddWmsModule(ModuleDescriptor.Create("picking").WithSettings(MaxTotes, Bulk));
        services.AddWmsModule(ModuleDescriptor.Create("mail").WithSettings(Password));
        using var provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<SettingRegistry>();

        Assert.Equal(["feature.bulk_picking", "picking.max_totes", "smtp.password"], registry.All.Select(k => k.Name));
        Assert.True(registry.TryGet("picking.max_totes", out var key));
        Assert.Same(MaxTotes, key);
        Assert.True(registry.Contains(MaxTotes));
        Assert.False(registry.Contains(new SettingKey<int>("picking.max_totes", 1, "an impostor")));
        Assert.False(registry.TryGet("picking.unknown", out _));
        Assert.False(registry.TryGet(null, out _));
        Assert.Contains(provider.GetRequiredService<ModuleCollection>().Modules, m => string.Equals(m.Name, "settings", StringComparison.Ordinal));
    }



    [Fact]
    public void Duplicate_names_across_modules_are_refused()
    {
        var modules = new ModuleCollection();
        modules.Add(ModuleDescriptor.Create("a").WithSettings(MaxTotes));
        modules.Add(ModuleDescriptor.Create("b").WithSettings(new SettingKey<int>("picking.max_totes", 2, "again")));

        var error = Assert.Throws<InvalidOperationException>(() => new SettingRegistry(modules));

        Assert.Contains("picking.max_totes", error.Message, StringComparison.Ordinal);
    }



    [Fact]
    public void Check_rejects_unknown_keys_disallowed_scopes_and_invalid_values()
    {
        var registry = new SettingRegistry([MaxTotes, Bulk]);

        Assert.Null(registry.Check("picking.max_totes", SettingScope.Zone, "3"));
        Assert.Equal(SettingErrorCodes.UnknownKey, registry.Check("picking.nope", SettingScope.Site, "3")?.Code);
        Assert.Equal(SettingErrorCodes.ScopeNotAllowed, registry.Check("feature.bulk_picking", SettingScope.Zone, "true")?.Code);
        Assert.Equal((SettingErrorCodes.InvalidValue, "must be positive"), registry.Check("picking.max_totes", SettingScope.Site, "0"));
        Assert.Equal(SettingErrorCodes.InvalidValue, registry.Check("picking.max_totes", SettingScope.Site, "many")?.Code);
        Assert.Equal(404, SettingErrorCodes.UnknownKey.HttpStatus);
    }



    [Fact]
    public void Descriptors_expose_the_registry_entry_and_mask_secret_defaults()
    {
        var totes = SettingDescriptor.Of(MaxTotes);
        var password = SettingDescriptor.Of(Password);

        Assert.Equal("picking.max_totes", totes.Name);
        Assert.Equal(SettingKind.Integer, totes.Kind);
        Assert.Equal(["organization", "site", "zone"], totes.Scopes);
        Assert.Equal("1", totes.Default);
        Assert.Null(totes.Choices);
        Assert.Equal(["organization"], password.Scopes);
        Assert.Equal("••••••", password.Default);
        Assert.Throws<ArgumentNullException>(() => SettingDescriptor.Of(null!));
    }



    [Fact]
    public void Constructors_reject_null()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingRegistry((ModuleCollection)null!));
        Assert.Throws<ArgumentNullException>(() => new SettingRegistry((IEnumerable<SettingKey>)null!));
        Assert.Throws<ArgumentException>(() => new SettingRegistry([MaxTotes, null!]));
        Assert.Throws<ArgumentNullException>(() => new SettingRegistry([MaxTotes]).Contains(null!));
        Assert.Throws<ArgumentNullException>(() => SettingsModule.AddWmsSettingsModule(null!));
    }
}
