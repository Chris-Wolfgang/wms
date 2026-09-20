// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// The <c>settings</c> module (E6): the registry (<c>GET /settings/registry</c>) and the values at a scope
/// (<c>GET/PUT/DELETE /settings/{scope}/{id}/{key}</c>), every write going through <see cref="ISettings"/>.
/// Writes to an existing row require <c>If-Match</c> with its entity tag (E5.2); the first write at a scope
/// has no row to match.
/// </summary>
public static class SettingsModule
{
    /// <summary>
    /// Route of the registry endpoint relative to the versioned API root.
    /// </summary>
    public const string RegistryRoute = "/settings/registry";



    /// <summary>
    /// Route of the values at one scope.
    /// </summary>
    public const string ScopeRoute = "/settings/{scopeType}/{scopeId:long}";



    /// <summary>
    /// Route of one value at one scope.
    /// </summary>
    public const string ValueRoute = ScopeRoute + "/{key}";



    /// <summary>
    /// The user recorded on writes until authentication arrives (E9).
    /// </summary>
    public const string AnonymousUser = "anonymous";



    /// <summary>
    /// The module descriptor: name <c>settings</c>, the endpoints, the module's error codes.
    /// </summary>
    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create("settings")
        .WithEndpoints(MapReadEndpoints)
        .WithEndpoints(MapWriteEndpoints)
        .WithErrorCodes(SettingErrorCodes.UnknownKey, SettingErrorCodes.ScopeNotAllowed, SettingErrorCodes.InvalidValue, SettingErrorCodes.UnknownScope, SettingErrorCodes.StoreUnavailable, SettingErrorCodes.ModeNotAllowed, SettingErrorCodes.DecidedElsewhere);



    /// <summary>
    /// Registers the module: the registry (built from every module registered by the time the host resolves
    /// it), the default accessor and hierarchy (replaced by <c>AddWmsDatabase</c>), and the error handler.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsSettingsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(provider => new SettingRegistry(provider.GetRequiredService<ModuleCollection>()));
        services.TryAddSingleton<ISettingScopeHierarchy, OrganizationOnlyScopeHierarchy>();
        services.TryAddScoped<ISettings, DefaultSettings>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddExceptionHandler<SettingExceptionHandler>();
        services.AddWmsModule(Descriptor);
        return services;
    }



    /// <summary>
    /// Parses the route's scope, refusing unknown scope types with <c>settings.unknown_scope</c>.
    /// </summary>
    /// <exception cref="SettingException"><paramref name="scopeType"/> is not a scope.</exception>
    public static SettingScopeRef ParseScope(string? scopeType, long scopeId)
    {
        if (!SettingScopeExtensions.TryParseScope(scopeType, out var type))
        {
            throw new SettingException(SettingErrorCodes.UnknownScope, $"'{scopeType}' is not a setting scope (organization, site, zone, sku).");
        }

        return new SettingScopeRef(type, type == SettingScope.Organization ? 0 : scopeId);
    }



    private static void MapReadEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(RegistryRoute, (SettingRegistry registry) => TypedResults.Ok(registry.All.Select(SettingDescriptor.Of).ToList()))
            .WithName("GetSettingRegistry")
            .WithSummary("Every setting the host knows: kind, scopes, default, description (read-only).");
        app.MapGet(ScopeRoute, async (string scopeType, long scopeId, ISettings settings, CancellationToken cancellationToken) =>
                TypedResults.Ok(await settings.ListAsync(ParseScope(scopeType, scopeId), cancellationToken).ConfigureAwait(false)))
            .WithName("ListSettings")
            .WithSummary("Every setting at a scope with its configured and effective value.");
        app.MapGet(ValueRoute, async (HttpContext http, string scopeType, long scopeId, string key, ISettings settings, CancellationToken cancellationToken) =>
                WithEtag(http, await settings.FindAsync(key, ParseScope(scopeType, scopeId), cancellationToken).ConfigureAwait(false)))
            .WithName("GetSetting")
            .WithSummary("One setting at a scope; the ETag is the stored row's version.");
    }



    private static void MapWriteEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPut(ValueRoute, async (HttpContext http, string scopeType, long scopeId, string key, SetSettingRequest body, ISettings settings, SettingRegistry registry, CancellationToken cancellationToken) =>
            {
                ArgumentNullException.ThrowIfNull(body);
                var scope = ParseScope(scopeType, scopeId);
                return await RequireCurrentAsync(http, key, scope, settings, cancellationToken).ConfigureAwait(false)
                    ?? WithEtag(http, await ApplyAsync(body, key, scope, settings, registry, User(http), cancellationToken).ConfigureAwait(false));
            })
            .WithName("SetSetting")
            .WithSummary("Configures a setting at a scope: a value, or a cascade mode (per_site, per_zone, per_sku); both null resets. If-Match required when a row exists.")
            .Produces<SettingValue>();
        app.MapDelete(ValueRoute, async (HttpContext http, string scopeType, long scopeId, string key, ISettings settings, CancellationToken cancellationToken) =>
            {
                var scope = ParseScope(scopeType, scopeId);
                return await RequireCurrentAsync(http, key, scope, settings, cancellationToken).ConfigureAwait(false)
                    ?? WithEtag(http, await settings.SetTextAsync(key, scope, text: null, User(http), cancellationToken).ConfigureAwait(false));
            })
            .WithName("ResetSetting")
            .WithSummary("Removes the configured value at a scope so it inherits again; If-Match required when a row exists.")
            .Produces<SettingValue>();
    }



    /// <summary>
    /// A mode delegates (E7.2); otherwise the value is written or, when null, the scope is reset.
    /// </summary>
    /// <exception cref="SettingException">The mode is not a cascade mode, or the key is not registered.</exception>
    private static Task<SettingValue> ApplyAsync(SetSettingRequest body, string key, SettingScopeRef scope, ISettings settings, SettingRegistry registry, string user, CancellationToken cancellationToken)
    {
        if (body.Mode is null)
        {
            return settings.SetTextAsync(key, scope, body.Value, user, cancellationToken);
        }

        if (!CascadeModeExtensions.TryParseMode(body.Mode, out var mode))
        {
            throw new SettingException(SettingErrorCodes.ModeNotAllowed, $"'{body.Mode}' is not a cascade mode (value, per_site, per_zone, per_sku).");
        }

        if (!registry.TryGet(key, out var registered))
        {
            throw new SettingException(SettingErrorCodes.UnknownKey, $"'{key}' is not a registered setting.");
        }

        return mode == CascadeMode.Value && body.Value is not null
            ? settings.SetTextAsync(key, scope, body.Value, user, cancellationToken)
            : settings.SetModeAsync(registered, scope, mode, user, cancellationToken);
    }



    private static async Task<IResult?> RequireCurrentAsync(HttpContext http, string key, SettingScopeRef scope, ISettings settings, CancellationToken cancellationToken)
    {
        var current = await settings.FindAsync(key, scope, cancellationToken).ConfigureAwait(false);
        return current.RowVersion is > 0 ? Preconditions.RequireIfMatch(http.Request, EntityTag.FromRowVersion((ulong)current.RowVersion.Value)) : null;
    }



    private static IResult WithEtag(HttpContext http, SettingValue value)
    {
        if (value.Etag is not null)
        {
            http.Response.Headers.ETag = value.Etag;
        }

        return TypedResults.Ok(value);
    }



    private static string User(HttpContext http)
    {
        return http.User.Identity?.Name is { Length: > 0 } name ? name : AnonymousUser;
    }
}
