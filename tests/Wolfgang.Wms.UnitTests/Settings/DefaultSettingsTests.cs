// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.UnitTests.Settings;

/// <summary>
/// E6.3 before a database exists: reads answer defaults, writes are refused with a problem code; plus the
/// value record's masking and entity tag, the exception, its handler, the placeholder hierarchy and the
/// route's scope parsing.
/// </summary>
public sealed class DefaultSettingsTests
{
    private static readonly SettingKey<int> MaxTotes = new("picking.max_totes", 3, "Totes a picker may carry.");
    private static readonly SettingKey<SecretText> Password = new("smtp.password", new SecretText("s3cret"), "SMTP password.") { Scopes = SettingScopes.Organization };
    private static readonly SettingRegistry Registry = new([MaxTotes, Password]);
    private static readonly DefaultSettings Settings = new(Registry);



    [Fact]
    public async Task Reads_answer_the_defaults()
    {
        var value = await Settings.GetAsync(MaxTotes, SettingScopeRef.Site(4), CancellationToken.None);
        var list = await Settings.ListAsync(SettingScopeRef.Organization, CancellationToken.None);
        var one = await Settings.FindAsync("smtp.password", SettingScopeRef.Organization, CancellationToken.None);

        Assert.Equal(3, value);
        Assert.Equal(["picking.max_totes", "smtp.password"], list.Select(v => v.Name));
        Assert.All(list, v => Assert.Equal(SettingValue.DefaultSource, v.InheritedFrom));
        Assert.Equal("3", list[0].EffectiveValue);
        Assert.Null(list[0].ConfiguredValue);
        Assert.Null(list[0].RowVersion);
        Assert.Null(list[0].Etag);
        Assert.Equal("••••••", one.EffectiveValue);
        Assert.Equal("organization", one.Scope);
    }



    [Fact]
    public async Task Writes_are_refused_until_a_store_exists_and_unknown_keys_are_refused_first()
    {
        var unregistered = new SettingKey<int>("picking.max_totes", 3, "impostor");

        await AssertCodeAsync(SettingErrorCodes.StoreUnavailable, () => Settings.SetAsync(MaxTotes, SettingScopeRef.Organization, 5, "me", CancellationToken.None));
        await AssertCodeAsync(SettingErrorCodes.StoreUnavailable, () => Settings.ResetAsync(MaxTotes, SettingScopeRef.Organization, "me", CancellationToken.None));
        await AssertCodeAsync(SettingErrorCodes.StoreUnavailable, () => Settings.SetTextAsync("picking.max_totes", SettingScopeRef.Organization, "5", "me", CancellationToken.None));
        await AssertCodeAsync(SettingErrorCodes.StoreUnavailable, () => Settings.SetModeAsync(MaxTotes, SettingScopeRef.Organization, CascadeMode.PerSite, "me", CancellationToken.None));
        await AssertCodeAsync(SettingErrorCodes.StoreUnavailable, () => Settings.PopulateAsync(SettingScopeRef.Site(1), "me", CancellationToken.None));
        await AssertCodeAsync(SettingErrorCodes.UnknownKey, () => Settings.SetModeAsync(unregistered, SettingScopeRef.Organization, CascadeMode.PerSite, "me", CancellationToken.None));
        await AssertCodeAsync(SettingErrorCodes.UnknownKey, () => Settings.SetTextAsync("picking.nope", SettingScopeRef.Organization, "5", "me", CancellationToken.None));
        await AssertCodeAsync(SettingErrorCodes.UnknownKey, () => Settings.FindAsync("picking.nope", SettingScopeRef.Organization, CancellationToken.None));
        await AssertCodeAsync(SettingErrorCodes.UnknownKey, () => Settings.GetAsync(unregistered, SettingScopeRef.Organization, CancellationToken.None));
        await AssertCodeAsync(SettingErrorCodes.UnknownKey, () => Settings.SetAsync(unregistered, SettingScopeRef.Organization, 1, "me", CancellationToken.None));
        await AssertCodeAsync(SettingErrorCodes.UnknownKey, () => Settings.ResetAsync(unregistered, SettingScopeRef.Organization, "me", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Settings.GetAsync<int>(null!, SettingScopeRef.Organization, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => new DefaultSettings(null!));
    }



    [Fact]
    public void Values_mask_secrets_and_carry_the_rows_entity_tag()
    {
        var plain = SettingValue.Create(MaxTotes, SettingScopeRef.Zone(9), "4", "4", null, rowVersion: 0x2A, updatedBy: "me", updatedAt: DateTimeOffset.UnixEpoch);
        var secret = SettingValue.Create(Password, SettingScopeRef.Organization, "hunter2", "hunter2", null, CascadeMode.Value, 7);
        var inherited = SettingValue.Create(Password, SettingScopeRef.Site(1), null, "hunter2", "organization");

        Assert.Equal("zone:9", plain.Scope);
        Assert.Equal("\"2a\"", plain.Etag);
        Assert.Equal(SettingKind.Integer, plain.Kind);
        Assert.Equal("••••••", secret.ConfiguredValue);
        Assert.Equal("••••••", secret.EffectiveValue);
        Assert.Null(inherited.ConfiguredValue);
        Assert.Equal("organization", inherited.InheritedFrom);
        Assert.Null(inherited.Etag);
        Assert.Equal("value", plain.CascadeMode);
        Assert.Equal(["value"], plain.AllowedModes);
        Assert.Equal(["value", "per_site", "per_zone"], SettingValue.Create(MaxTotes, SettingScopeRef.Organization, null, "3", "default").AllowedModes);
        Assert.Equal(["value"], secret.AllowedModes);
        Assert.Throws<ArgumentNullException>(() => SettingValue.Create(null!, SettingScopeRef.Organization, null, "x", null));
        Assert.Throws<ArgumentNullException>(() => SettingValue.Create(MaxTotes, SettingScopeRef.Organization, null, null!, null));
    }



    [Fact]
    public async Task The_exception_carries_its_code_and_the_handler_answers_with_it()
    {
        var handler = new SettingExceptionHandler();
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider() };
        context.Response.Body = new MemoryStream();
        var inner = new InvalidOperationException("cause");

        var handled = await handler.TryHandleAsync(context, new SettingException(SettingErrorCodes.UnknownKey, "'x' is not a registered setting."), CancellationToken.None);
        var ignored = await handler.TryHandleAsync(context, inner, CancellationToken.None);
        var withCause = new SettingException(SettingErrorCodes.InvalidValue, "bad", inner);

        Assert.True(handled);
        Assert.Equal(404, context.Response.StatusCode);
        Assert.False(ignored);
        Assert.Same(inner, withCause.InnerException);
        Assert.Equal("bad", withCause.Message);
        Assert.Throws<ArgumentNullException>(() => new SettingException(null!, "d"));
        Assert.Throws<ArgumentNullException>(() => new SettingException(null!, "d", inner));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await handler.TryHandleAsync(null!, inner, CancellationToken.None));
    }



    [Fact]
    public async Task The_placeholder_hierarchy_knows_only_that_sites_belong_to_the_organisation()
    {
        var hierarchy = new OrganizationOnlyScopeHierarchy();

        Assert.Null(await hierarchy.ParentAsync(SettingScopeRef.Organization, CancellationToken.None));
        Assert.Equal(SettingScopeRef.Organization, await hierarchy.ParentAsync(SettingScopeRef.Site(3), CancellationToken.None));
        Assert.Null(await hierarchy.ParentAsync(SettingScopeRef.Zone(3), CancellationToken.None));
        Assert.Null(await hierarchy.ParentAsync(SettingScopeRef.Sku(3), CancellationToken.None));
        Assert.Empty(await hierarchy.ChildrenAsync(SettingScopeRef.Organization, CancellationToken.None));
    }



    [Fact]
    public void Route_scopes_parse_by_stored_name_and_the_organisation_ignores_the_id()
    {
        Assert.Equal(SettingScopeRef.Organization, SettingsModule.ParseScope("organization", 42));
        Assert.Equal(SettingScopeRef.Site(7), SettingsModule.ParseScope("SITE", 7));
        var failure = Assert.Throws<SettingException>(() => SettingsModule.ParseScope("aisle", 1));
        Assert.Equal(SettingErrorCodes.UnknownScope, failure.Code);
    }



    private static async Task AssertCodeAsync(ErrorCode expected, Func<Task> action)
    {
        var failure = await Assert.ThrowsAsync<SettingException>(action);
        Assert.Equal(expected, failure.Code);
    }
}
