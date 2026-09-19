// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Localization;

namespace Wolfgang.Wms.UnitTests.Localization;

public sealed class WmsLocalizationTests
{
    [Fact]
    public void English_is_the_only_shipped_culture_and_the_default()
    {
        Assert.Equal(["en"], WmsLocalization.SupportedCultures);
        Assert.Equal("en", WmsLocalization.DefaultCulture);
        Assert.Equal("Resources", WmsLocalization.ResourcesPath);
    }



    [Fact]
    public void AddWmsLocalization_configures_request_cultures_from_the_supported_list()
    {
        using var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;

        Assert.Equal("en", options.DefaultRequestCulture.Culture.Name);
        Assert.Equal(["en"], options.SupportedCultures!.Select(c => c.Name));
        Assert.Equal(["en"], options.SupportedUICultures!.Select(c => c.Name));
        Assert.True(options.FallBackToParentCultures);
        Assert.True(options.FallBackToParentUICultures);
        Assert.True(options.ApplyCurrentCultureToResponseHeaders);
    }



    [Fact]
    public void AddWmsLocalization_registers_resource_file_localizers_under_the_Resources_folder()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<IStringLocalizerFactory>());
        Assert.Equal("Resources", provider.GetRequiredService<IOptions<LocalizationOptions>>().Value.ResourcesPath);
    }



    [Theory]
    [InlineData(null, "en")]
    [InlineData("de-DE, de;q=0.9", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("en", "en")]
    public async Task UseWmsRequestLocalization_resolves_the_request_culture_with_parent_fallback(string? acceptLanguage, string expected)
    {
        using var provider = BuildProvider();
        string? resolved = null;
        var pipeline = new ApplicationBuilder(provider)
            .UseWmsRequestLocalization()
            .Terminate(_ =>
            {
                resolved = CultureInfo.CurrentUICulture.Name;
                return Task.CompletedTask;
            });
        var context = new DefaultHttpContext { RequestServices = provider };
        if (acceptLanguage is not null)
        {
            context.Request.Headers.AcceptLanguage = acceptLanguage;
        }

        await pipeline(context);

        Assert.Equal(expected, resolved);
        Assert.Equal(expected, context.Response.Headers.ContentLanguage);
    }



    [Fact]
    public void Extensions_when_the_argument_is_null_throw()
    {
        Assert.Throws<ArgumentNullException>(() => WmsLocalization.AddWmsLocalization(null!));
        Assert.Throws<ArgumentNullException>(() => WmsLocalization.UseWmsRequestLocalization(null!));
    }



    private static ServiceProvider BuildProvider()
    {
        return new ServiceCollection()
            .AddLogging()
            .AddWmsLocalization()
            .BuildServiceProvider();
    }
}



file static class ApplicationBuilderExtensions
{
    public static RequestDelegate Terminate(this IApplicationBuilder app, RequestDelegate terminal)
    {
        app.Use(_ => terminal);
        return app.Build();
    }
}
