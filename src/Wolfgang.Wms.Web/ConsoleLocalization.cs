// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Wolfgang.Wms.Domain.Localization;

namespace Wolfgang.Wms.Web;

/// <summary>
/// The console's localization wiring (E82.4), the same policy as the API's <c>WmsLocalization</c> (E1.14) and
/// fed from the same list (<see cref="Cultures"/>): the console may not reference Core (E82.1), so it registers
/// its own. Strings come from <c>Resources/ConsoleText.resx</c> in <c>Web.Shared</c>.
/// </summary>
public static class ConsoleLocalization
{
    /// <summary>
    /// Registers resource-file localization and the request-culture options: supported and default cultures from
    /// <see cref="Cultures"/>, the user's picker (cookie) before the browser's <c>Accept-Language</c>,
    /// parent-culture fallback and the resolved culture echoed in <c>Content-Language</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddConsoleLocalization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLocalization(options => options.ResourcesPath = Cultures.ResourcesPath);
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var cultures = Cultures.Supported.Select(CultureInfo.GetCultureInfo).ToList();
            options.DefaultRequestCulture = new RequestCulture(Cultures.Default);
            options.SupportedCultures = cultures;
            options.SupportedUICultures = cultures;
            options.FallBackToParentCultures = true;
            options.FallBackToParentUICultures = true;
            options.ApplyCurrentCultureToResponseHeaders = true;

            // The default list also reads ?culture= first; the policy is the picker cookie, then the browser.
            options.RequestCultureProviders = [new CookieRequestCultureProvider(), new AcceptLanguageHeaderRequestCultureProvider()];
        });
        return services;
    }
}
