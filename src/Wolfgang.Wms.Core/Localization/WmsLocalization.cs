// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Wolfgang.Wms.Core.Localization;

/// <summary>
/// Localization wiring shared by every HTTP host (E1.14). The infrastructure exists from the start; English is
/// the only shipped language in v1 and adding a culture is one entry in <see cref="SupportedCultures"/> plus
/// the resource files.
/// </summary>
public static class WmsLocalization
{
    /// <summary>
    /// The culture used when the request names none of the supported ones.
    /// </summary>
    public const string DefaultCulture = "en";



    /// <summary>
    /// Folder, relative to each host project, that holds the <c>.resx</c> files.
    /// </summary>
    public const string ResourcesPath = "Resources";



    /// <summary>
    /// Every culture the product ships strings for. Order is the preference order for fallback.
    /// </summary>
    public static IReadOnlyList<string> SupportedCultures { get; } = [DefaultCulture];



    /// <summary>
    /// Registers resource-file localization and the request-culture options: supported and default cultures
    /// from <see cref="SupportedCultures"/>, culture taken from the user's picker (cookie) before the
    /// browser's <c>Accept-Language</c>, parent-culture fallback (<c>en-GB</c> resolves to <c>en</c>) and the
    /// resolved culture echoed in <c>Content-Language</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsLocalization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLocalization(options => options.ResourcesPath = ResourcesPath);
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var cultures = SupportedCultures.Select(CultureInfo.GetCultureInfo).ToList();
            options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(DefaultCulture);
            options.SupportedCultures = cultures;
            options.SupportedUICultures = cultures;
            options.FallBackToParentCultures = true;
            options.FallBackToParentUICultures = true;
            options.ApplyCurrentCultureToResponseHeaders = true;
        });

        return services;
    }



    /// <summary>
    /// Adds the request-localization middleware configured by <see cref="AddWmsLocalization"/>. Call before
    /// any middleware that renders text.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IApplicationBuilder UseWmsRequestLocalization(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseRequestLocalization();
    }
}
