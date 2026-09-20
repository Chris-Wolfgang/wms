// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.UnitTests.Settings;

/// <summary>
/// E6.1: a key carries kind, allowed scopes, default, description, validator, restart and resync flags,
/// and validates stored text through its codec and validator.
/// </summary>
public sealed class SettingKeyTests
{
    private static readonly SettingKey<TimeSpan> LeaseTimeout = new("picking.lease_timeout", TimeSpan.FromMinutes(15), "How long a picker holds a task.")
    {
        Validator = v => v >= TimeSpan.FromMinutes(1) && v <= TimeSpan.FromHours(1) ? null : "must be between 1 minute and 1 hour",
        TriggersDeviceResync = true,
    };



    [Fact]
    public void Defaults_are_organisation_to_zone_no_restart_no_resync()
    {
        var key = new SettingKey<int>("picking.max_totes", 1, "Totes a picker may carry.");

        Assert.Equal(SettingKind.Integer, key.Kind);
        Assert.Equal(SettingScopes.OrganizationToZone, key.Scopes);
        Assert.True(key.AllowsScope(SettingScope.Zone));
        Assert.False(key.AllowsScope(SettingScope.Sku));
        Assert.False(key.RequiresRestart);
        Assert.False(key.TriggersDeviceResync);
        Assert.Equal("1", key.DefaultText);
        Assert.Null(key.Choices);
        Assert.Null(key.Validator);
    }



    [Fact]
    public void Metadata_is_set_with_init_properties()
    {
        var key = new SettingKey<SecretText>("smtp.password", new SecretText(string.Empty), "SMTP password.")
        {
            Scopes = SettingScopes.Organization,
            RequiresRestart = true,
        };

        Assert.Equal(SettingKind.Secret, key.Kind);
        Assert.True(key.AllowsScope(SettingScope.Organization));
        Assert.False(key.AllowsScope(SettingScope.Site));
        Assert.True(key.RequiresRestart);
        Assert.True(LeaseTimeout.TriggersDeviceResync);
        Assert.Equal("00:15:00", LeaseTimeout.DefaultText);
    }



    [Theory]
    [InlineData("00:30:00", null)]
    [InlineData("00:00:30", "must be between 1 minute and 1 hour")]
    [InlineData("half an hour", "'half an hour' is not a valid duration value for picking.lease_timeout.")]
    [InlineData(null, "'' is not a valid duration value for picking.lease_timeout.")]
    public void Validate_parses_through_the_codec_then_runs_the_validator(string? text, string? expected)
    {
        Assert.Equal(expected, LeaseTimeout.Validate(text));
    }



    [Fact]
    public void Typed_validate_runs_the_validator_alone()
    {
        Assert.Null(LeaseTimeout.Validate(TimeSpan.FromMinutes(5)));
        Assert.Equal("must be between 1 minute and 1 hour", LeaseTimeout.Validate(TimeSpan.FromHours(2)));
        Assert.Null(new SettingKey<int>("a.b", 1, "d").Validate(99));
    }



    [Fact]
    public void A_key_with_a_custom_codec_reports_that_kind()
    {
        var codec = SettingCodecs.Json<int[]>(v => string.Join(',', v), t => t.Split(',').Select(int.Parse).ToArray());
        var key = new SettingKey<int[]>("picking.priorities", [1, 2], "Priority order.", codec);

        Assert.Equal(SettingKind.Json, key.Kind);
        Assert.Equal("1,2", key.DefaultText);
        Assert.Same(codec, key.Codec);
        Assert.Throws<ArgumentNullException>(() => new SettingKey<int[]>("picking.priorities", [1], "d", null!));
        Assert.Throws<NotSupportedException>(() => new SettingKey<int[]>("picking.priorities", [1], "d"));
    }



    [Fact]
    public void Enum_keys_list_their_choices()
    {
        var key = new SettingKey<SettingScope>("sample.scope", SettingScope.Site, "A sample enum setting.");

        Assert.Equal(SettingKind.Enum, key.Kind);
        Assert.Equal(["Organization", "Site", "Zone", "Sku"], key.Choices);
        Assert.Equal("Site", key.DefaultText);
        Assert.Null(key.Validate("zone"));
        Assert.NotNull(key.Validate("aisle"));
    }
}
