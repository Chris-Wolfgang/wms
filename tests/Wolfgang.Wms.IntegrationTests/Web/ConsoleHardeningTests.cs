// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Wolfgang.Wms.Web;

namespace Wolfgang.Wms.IntegrationTests.Web;

/// <summary>
/// E10.6 on the console's own pieces: the proxy flag drives forwarded headers the same way as the API, and
/// the security-header middleware sets the documented set on every response.
/// </summary>
public sealed class ConsoleHardeningTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void The_console_honours_forwarded_headers_only_behind_a_proxy(string? flag, bool expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [ConsoleHosting.BehindProxyKey] = flag }).Build();
        var options = new ForwardedHeadersOptions();

        ConsoleHosting.ConfigureForwardedHeaders(options, configuration);

        Assert.Equal(expected, options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto));
        if (expected)
        {
            Assert.Empty(options.KnownIPNetworks);
        }

        Assert.Throws<ArgumentNullException>(() => ConsoleHosting.ConfigureForwardedHeaders(null!, configuration));
        Assert.Throws<ArgumentNullException>(() => ConsoleHosting.ConfigureForwardedHeaders(options, null!));
    }



    [Fact]
    public void Security_headers_are_the_documented_set()
    {
        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, SecurityHeaders.Headers["Content-Security-Policy"]);
        Assert.Equal("nosniff", SecurityHeaders.Headers["X-Content-Type-Options"]);
        Assert.Equal("strict-origin-when-cross-origin", SecurityHeaders.Headers["Referrer-Policy"]);
        Assert.Equal("DENY", SecurityHeaders.Headers["X-Frame-Options"]);
        Assert.Contains("frame-ancestors 'none'", SecurityHeaders.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.Contains("wss:", SecurityHeaders.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.Equal(5, SecurityHeaders.Headers.Count);
        Assert.Throws<ArgumentNullException>(() => SecurityHeaders.UseWmsSecurityHeaders(null!));
    }
}
