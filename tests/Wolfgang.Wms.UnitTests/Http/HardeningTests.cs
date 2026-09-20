// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Hosting;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Identity;
using HttpOverrides = Microsoft.AspNetCore.HttpOverrides;

namespace Wolfgang.Wms.UnitTests.Http;

/// <summary>
/// E10.6 without a host: forwarded headers are honoured only behind a proxy; the CORS setting parses and
/// validates origins and builds the policy.
/// </summary>
public sealed class HardeningTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void Forwarded_headers_are_honoured_only_behind_a_proxy(string? flag, bool expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [HostingOptions.BehindProxyKey] = flag }).Build();
        var services = new ServiceCollection();
        services.AddWmsForwardedHeaders(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>>().Value;

        Assert.Equal(expected, provider.GetRequiredService<HostingOptions>().BehindProxy);
        Assert.Equal(expected, options.ForwardedHeaders.HasFlag(HttpOverrides.ForwardedHeaders.XForwardedProto));
        if (expected)
        {
            Assert.Empty(options.KnownIPNetworks);
            Assert.Empty(options.KnownProxies);
        }
    }



    [Fact]
    public void Forwarded_header_guards()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<ArgumentNullException>(() => ForwardedHeaders.AddWmsForwardedHeaders(null!, configuration));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddWmsForwardedHeaders(null!));
        Assert.Throws<ArgumentNullException>(() => ForwardedHeaders.Configure(null!, new HostingOptions()));
        Assert.Throws<ArgumentNullException>(() => ForwardedHeaders.Configure(new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions(), null!));
        Assert.Throws<ArgumentNullException>(() => ForwardedHeaders.UseWmsForwardedHeaders(null!));
    }



    [Fact]
    public void Cors_origins_parse_validate_and_build_the_policy()
    {
        Assert.Equal(["https://apps.example.com", "http://localhost:5173"], WmsCors.Parse(" https://apps.example.com, http://localhost:5173 ,https://APPS.example.com,"));
        Assert.Empty(WmsCors.Parse(null));
        Assert.Null(WmsCors.AllowedOrigins.Validate("https://apps.example.com, http://localhost:5173"));
        Assert.Null(WmsCors.AllowedOrigins.Validate(string.Empty));
        Assert.Contains("not an origin", WmsCors.AllowedOrigins.Validate("https://apps.example.com/path")!, StringComparison.Ordinal);
        Assert.Contains("not an origin", WmsCors.AllowedOrigins.Validate("ftp://apps.example.com")!, StringComparison.Ordinal);
        Assert.Contains("not an origin", WmsCors.AllowedOrigins.Validate("apps")!, StringComparison.Ordinal);
        Assert.Contains(WmsCors.AllowedOrigins, AuthSettings.All);

        var none = WmsCors.PolicyFor([]);
        var some = WmsCors.PolicyFor(["https://apps.example.com"]);

        Assert.Empty(none.Origins);
        Assert.False(none.SupportsCredentials);
        Assert.Equal(["https://apps.example.com"], some.Origins);
        Assert.True(some.SupportsCredentials);
        Assert.True(some.AllowAnyHeader);
        Assert.True(some.AllowAnyMethod);
        Assert.Contains("ETag", some.ExposedHeaders);
        Assert.Throws<ArgumentNullException>(() => WmsCors.PolicyFor(null!));
        Assert.Throws<ArgumentNullException>(() => WmsCors.AddWmsCors(null!));
        Assert.Throws<ArgumentNullException>(() => WmsCors.UseWmsCors(null!));
    }
}
