// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Http;

/// <summary>
/// Cross-origin access for customer browser apps (E10.6): an allow-list of origins kept as a setting, read
/// on every request through the settings cache, so a change applies without a restart. No origin is
/// allowed until one is listed; listed origins may send credentials (the session cookie) and read the
/// <c>ETag</c>.
/// </summary>
public static class WmsCors
{
    /// <summary>
    /// The allowed origins, comma-separated (<c>https://apps.example.com, https://kiosk.example.com</c>);
    /// empty for none.
    /// </summary>
    public static readonly SettingKey<string> AllowedOrigins = new("api.cors.allowed_origins", string.Empty, "Browser origins allowed to call the API, comma-separated; empty for none.")
    {
        Scopes = SettingScopes.Organization,
        Validator = Validate,
    };



    /// <summary>
    /// Registers CORS with the settings-backed policy.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsCors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCors();
        services.AddSingleton<ICorsPolicyProvider, SettingsCorsPolicyProvider>();
        return services;
    }



    /// <summary>
    /// The middleware; before authentication so preflights are answered without a session.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IApplicationBuilder UseWmsCors(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseCors();
    }



    /// <summary>
    /// The origins in a setting value: trimmed, distinct, in order.
    /// </summary>
    public static IReadOnlyList<string> Parse(string? value)
    {
        return (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }



    /// <summary>
    /// The policy for an origin list: those origins, any method and header, credentials, <c>ETag</c> exposed.
    /// </summary>
    public static CorsPolicy PolicyFor(IReadOnlyList<string> origins)
    {
        ArgumentNullException.ThrowIfNull(origins);

        var builder = new CorsPolicyBuilder().AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("ETag");
        if (origins.Count > 0)
        {
            builder.WithOrigins([.. origins]).AllowCredentials();
        }

        return builder.Build();
    }



    private static string? Validate(string value)
    {
        foreach (var origin in Parse(value))
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || !(string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)) || !string.Equals(uri.PathAndQuery, "/", StringComparison.Ordinal) || !string.IsNullOrEmpty(uri.Fragment))
            {
                return $"'{origin}' is not an origin (scheme://host[:port], no path).";
            }
        }

        return null;
    }



    private sealed class SettingsCorsPolicyProvider : ICorsPolicyProvider
    {
        public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
        {
            ArgumentNullException.ThrowIfNull(context);

            var settings = context.RequestServices.GetRequiredService<ISettings>();
            var value = await settings.GetAsync(AllowedOrigins, SettingScopeRef.Organization, context.RequestAborted).ConfigureAwait(false);
            return PolicyFor(Parse(value));
        }
    }
}
