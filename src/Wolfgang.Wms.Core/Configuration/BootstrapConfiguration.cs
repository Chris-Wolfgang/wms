// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Wolfgang.Wms.Core.Configuration;

/// <summary>
/// The bootstrap keys (E6.5): everything <c>appsettings</c> may hold. The process needs these before it can
/// reach the database; everything else is a setting edited in the Configure workspace (E6). A key in an
/// <c>appsettings*.json</c> file that is not listed here, or a <c>Wms:</c> key from any other provider
/// (an environment variable, the command line), is ignored and named in a startup warning with its source,
/// so a setting typed into the wrong place is noticed instead of silently doing nothing.
/// </summary>
public static class BootstrapConfiguration
{
    /// <summary>
    /// Keys recognised exactly (case-insensitive).
    /// </summary>
    public static IReadOnlyList<string> RecognizedKeys { get; } =
    [
        "AllowedHosts",
        "Urls",
        "Wms:Database:Provider",
        "Wms:Database:ConnectionString",
        "Wms:Database:TrustServerCertificate",
        "Wms:DataProtection:KeyRingPath",   // E8.1
        "Wms:Bootstrap:AdminUserName",      // E9.1: the bootstrap administrator's name (default admin)
        "Wms:Hosting:BehindProxy",          // E10.6: honour X-Forwarded-* from the reverse proxy
        "Wms:Auth:ForceLocal",              // E11.0: emergency override, local sign-in only
    ];



    /// <summary>
    /// Sections recognised whole (case-insensitive): the framework's own.
    /// </summary>
    public static IReadOnlyList<string> RecognizedSections { get; } = ["Logging", "Kestrel"];



    /// <summary>
    /// Every unrecognised leaf key, as <c>Key (source)</c>, sorted: from an <c>appsettings</c> JSON provider,
    /// every leaf that is neither a recognised key nor under a recognised section; from any other provider
    /// (environment variables, the command line, a chained host configuration), every such leaf under
    /// <c>Wms:</c>, since the rest of those providers is the platform's own. The source is the file, or the
    /// provider's kind when there is no file.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
    public static IReadOnlyList<string> UnrecognizedKeys(IConfigurationRoot configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var unrecognized = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in configuration.Providers)
        {
            var (source, ownKeysOnly) = provider switch
            {
                JsonConfigurationProvider file => (file.Source.Path ?? "appsettings", false),
                JsonStreamConfigurationProvider => ("appsettings (stream)", false),
                _ => (KindOf(provider), true),
            };

            foreach (var key in LeafKeys(provider, parentPath: null).Where(key => !IsRecognized(key) && (!ownKeysOnly || IsOwn(key))))
            {
                unrecognized.Add(key + " (" + source + ")");
            }
        }

        return [.. unrecognized];
    }



    /// <summary>
    /// True when <paramref name="key"/> is a recognised bootstrap key or lies under a recognised section.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public static bool IsRecognized(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return RecognizedKeys.Contains(key, StringComparer.OrdinalIgnoreCase)
            || RecognizedSections.Any(section => key.StartsWith(section + ":", StringComparison.OrdinalIgnoreCase) || string.Equals(key, section, StringComparison.OrdinalIgnoreCase));
    }



    /// <summary>
    /// Registers the startup check that logs the unrecognised keys (never fails startup).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsBootstrapConfigurationCheck(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHostedService<BootstrapConfigurationCheck>();
        return services;
    }



    /// <summary>
    /// True when <paramref name="key"/> is the product's (<c>Wms:</c>), the only keys of a non-file provider
    /// that can be a mistyped setting rather than the platform's own.
    /// </summary>
    private static bool IsOwn(string key)
    {
        return key.StartsWith("Wms:", StringComparison.OrdinalIgnoreCase);
    }



    /// <summary>
    /// A provider's kind for the warning: its type name without the <c>ConfigurationProvider</c> suffix
    /// (<c>EnvironmentVariables</c>, <c>CommandLine</c>, <c>Memory</c>, <c>Chained</c>).
    /// </summary>
    private static string KindOf(IConfigurationProvider provider)
    {
        const string suffix = "ConfigurationProvider";
        var name = provider.GetType().Name;
        return name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;
    }



    private static IEnumerable<string> LeafKeys(IConfigurationProvider provider, string? parentPath)
    {
        foreach (var child in provider.GetChildKeys([], parentPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = parentPath is null ? child : parentPath + ":" + child;
            if (provider.TryGet(path, out _))
            {
                yield return path;
                continue;
            }

            foreach (var leaf in LeafKeys(provider, path))
            {
                yield return leaf;
            }
        }
    }
}
