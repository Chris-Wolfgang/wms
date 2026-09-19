// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Asp.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Api;

namespace Wolfgang.Wms.UnitTests.Api;

public sealed class WmsApiTests
{
    [Fact]
    public void V0_is_the_only_served_version_and_nothing_is_frozen_before_1_0()
    {
        Assert.Equal(new ApiVersion(0), WmsApi.V0);
        Assert.Equal(WmsApi.V0, WmsApi.Current);
        Assert.Equal([WmsApi.V0], WmsApi.Served);
        Assert.Empty(WmsApi.Frozen);
        Assert.Equal("/api/v{version:apiVersion}", WmsApi.RouteTemplate);
    }



    [Fact]
    public void DocumentName_is_v_plus_the_major_version()
    {
        Assert.Equal("v0", WmsApi.DocumentName(WmsApi.V0));
        Assert.Equal("v1", WmsApi.DocumentName(new ApiVersion(1)));
        Assert.Throws<ArgumentNullException>(() => WmsApi.DocumentName(null!));
    }



    [Fact]
    public void AddWmsApiVersioning_requires_the_version_in_the_path_and_reports_supported_versions()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddWmsApiVersioning()
            .BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ApiVersioningOptions>>().Value;

        Assert.Equal(WmsApi.V0, options.DefaultApiVersion);
        Assert.False(options.AssumeDefaultVersionWhenUnspecified);
        Assert.True(options.ReportApiVersions);
        Assert.IsType<UrlSegmentApiVersionReader>(options.ApiVersionReader);
    }



    [Fact]
    public void SubstituteVersion_replaces_the_version_placeholder_in_a_path()
    {
        Assert.Equal("/api/v0/skus/{skuCode}", WmsApi.SubstituteVersion("/api/v{version}/skus/{skuCode}", WmsApi.V0));
        Assert.Equal("/health", WmsApi.SubstituteVersion("/health", WmsApi.V0));
    }



    [Fact]
    public void Extensions_when_the_argument_is_null_throw()
    {
        Assert.Throws<ArgumentNullException>(() => WmsApi.AddWmsApiVersioning(null!));
        Assert.Throws<ArgumentNullException>(() => WmsApi.MapWmsApi(null!));
    }
}
