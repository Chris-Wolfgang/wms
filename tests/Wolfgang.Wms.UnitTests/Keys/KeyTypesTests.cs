// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.UnitTests.Keys;

public sealed class KeyTypesTests
{
    [Theory]
    [InlineData("picking")]
    [InlineData("picking.lease_timeout")]
    [InlineData("devices.remote-logging2")]
    [InlineData("a.b.c")]
    public void Valid_key_names_are_lower_case_dotted_identifiers(string name)
    {
        Assert.True(KeyName.IsValid(name));
        Assert.Equal(name, KeyName.Require(name, "name"));
    }



    [Theory]
    [InlineData("")]
    [InlineData("Picking")]
    [InlineData("picking.")]
    [InlineData(".picking")]
    [InlineData("1picking")]
    [InlineData("picking lease")]
    [InlineData("picking..lease")]
    [InlineData("pick/ing")]
    public void Invalid_key_names_are_rejected(string name)
    {
        Assert.False(KeyName.IsValid(name));
        Assert.Throws<ArgumentException>(() => KeyName.Require(name, "name"));
    }



    [Fact]
    public void Feature_flag_maps_to_a_feature_prefixed_setting_name()
    {
        var flag = new FeatureFlag("bulk_picking");

        Assert.Equal("feature.bulk_picking", flag.SettingName);
        Assert.Null(flag.RemoveInRelease);
    }



    [Fact]
    public void Ship_dark_feature_flag_records_its_removal_release()
    {
        var flag = new FeatureFlag("new_task_list", RemoveInRelease: "0.3.0");

        Assert.Equal("0.3.0", flag.RemoveInRelease);
    }



    [Fact]
    public void Typed_setting_key_carries_type_default_and_description()
    {
        var key = new SettingKey<TimeSpan>("picking.lease_timeout", TimeSpan.FromMinutes(15), "How long a picker holds a task.");

        Assert.Equal(typeof(TimeSpan), key.ValueType);
        Assert.Equal(TimeSpan.FromMinutes(15), key.DefaultValue);
        Assert.Equal("How long a picker holds a task.", key.Description);
        Assert.IsAssignableFrom<SettingKey>(key);
    }



    [Fact]
    public void Setting_key_requires_a_description()
    {
        Assert.Throws<ArgumentException>(() => new SettingKey<int>("picking.max_totes", 1, " "));
    }



    [Fact]
    public void Keys_are_value_equal_by_content()
    {
        Assert.Equal(new Permission("picking.release", "Release a wave"), new Permission("picking.release", "Release a wave"));
        Assert.NotEqual(new JobName("core.retention", "Purge old data"), new JobName("core.backup", "Back up the database"));
        Assert.Equal(new SettingKey<int>("a.b", 1, "d"), new SettingKey<int>("a.b", 1, "d"));
    }



    [Theory]
    [InlineData("Picking.Release", "desc")]
    [InlineData("picking.release", "")]
    public void Named_keys_validate_name_and_description(string name, string description)
    {
        Assert.Throws<ArgumentException>(() => new Permission(name, description));
        Assert.Throws<ArgumentException>(() => new LicenseLimit(name, description));
        Assert.Throws<ArgumentException>(() => new LicenseFeature(name, description));
        Assert.Throws<ArgumentException>(() => new IssueType(name, description));
        Assert.Throws<ArgumentException>(() => new JobName(name, description));
    }



    [Fact]
    public void Error_code_carries_status_message_anchor_and_severity()
    {
        var code = new ErrorCode("picking.tote_already_closed", 409, "Tote {tote} is already closed.", "tote-already-closed", ErrorSeverity.Error);

        Assert.Equal(409, code.HttpStatus);
        Assert.Equal(ErrorSeverity.Error, code.Severity);
        Assert.Equal("tote-already-closed", code.DocsAnchor);
    }



    [Theory]
    [InlineData(99)]
    [InlineData(600)]
    public void Error_code_rejects_an_impossible_http_status(int status)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ErrorCode("a.b", status, "m", "a", ErrorSeverity.Error));
    }



    [Theory]
    [InlineData("", "anchor")]
    [InlineData("message", " ")]
    public void Error_code_requires_message_and_anchor(string message, string anchor)
    {
        Assert.Throws<ArgumentException>(() => new ErrorCode("a.b", 400, message, anchor, ErrorSeverity.Warning));
    }
}
