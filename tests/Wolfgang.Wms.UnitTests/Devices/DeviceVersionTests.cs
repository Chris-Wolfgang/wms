// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Devices;

namespace Wolfgang.Wms.UnitTests.Devices;

public sealed class DeviceVersionTests
{
    [Theory]
    [InlineData("0.4.2", "0.4.2")]
    [InlineData(" 1.2 ", "1.2")]
    [InlineData("0.4.3-alpha.0.7", "0.4.3")]
    [InlineData("1.0.0+abc123", "1.0.0")]
    [InlineData("2.1.0.5", "2.1.0.5")]
    public void TryParse_reads_the_numeric_prefix(string header, string expected)
    {
        var ok = DeviceVersion.TryParse(header, out var version);

        Assert.True(ok);
        Assert.Equal(Version.Parse(expected), version);
    }



    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("latest")]
    [InlineData("1")]
    [InlineData("-alpha")]
    public void TryParse_rejects_text_that_is_not_a_version(string? header)
    {
        Assert.False(DeviceVersion.TryParse(header, out _));
    }



    [Theory]
    [InlineData("1.4", "1.4.0", true)]
    [InlineData("1.4.0", "1.4", true)]
    [InlineData("1.4.1", "1.4.0", true)]
    [InlineData("1.3.9", "1.4.0", false)]
    [InlineData("2.0", "1.9.9", true)]
    [InlineData("1.4.0.1", "1.4.0", true)]
    public void Satisfies_compares_with_missing_parts_as_zero(string version, string minimum, bool expected)
    {
        Assert.Equal(expected, DeviceVersion.Satisfies(Version.Parse(version), Version.Parse(minimum)));
    }



    [Fact]
    public void Satisfies_when_an_argument_is_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => DeviceVersion.Satisfies(null!, new Version(1, 0)));
        Assert.Throws<ArgumentNullException>(() => DeviceVersion.Satisfies(new Version(1, 0), null!));
    }



    [Fact]
    public async Task The_default_policy_has_no_minimum_and_is_registered_by_AddWmsDeviceVersioning()
    {
        using var provider = new ServiceCollection()
            .AddWmsDeviceVersioning()
            .BuildServiceProvider();

        var policy = provider.GetRequiredService<IDeviceVersionPolicy>();

        Assert.IsType<NoMinimumDeviceVersionPolicy>(policy);
        Assert.Null(await policy.GetMinimumAsync(CancellationToken.None));
    }



    [Fact]
    public void Error_codes_carry_the_expected_statuses()
    {
        Assert.Equal(400, DeviceErrorCodes.VersionMissing.HttpStatus);
        Assert.Equal(400, DeviceErrorCodes.VersionInvalid.HttpStatus);
        Assert.Equal(426, DeviceErrorCodes.VersionTooOld.HttpStatus);
        Assert.Equal("X-Wms-Device-Version", DeviceVersion.HeaderName);
    }



    [Fact]
    public void Extensions_and_filter_when_the_argument_is_null_throw()
    {
        Assert.Throws<ArgumentNullException>(() => DeviceVersionConventions.AddWmsDeviceVersioning(null!));
        Assert.Throws<ArgumentNullException>(() => DeviceVersionConventions.RequireDeviceVersion<Microsoft.AspNetCore.Routing.RouteGroupBuilder>(null!));
        Assert.Throws<ArgumentNullException>(() => new DeviceVersionFilter(null!));
    }
}
