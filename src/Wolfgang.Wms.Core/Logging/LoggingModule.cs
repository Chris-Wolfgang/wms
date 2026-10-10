// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;
using Wolfgang.Wms.Logging;

namespace Wolfgang.Wms.Core.Logging;

/// <summary>
/// The <c>logging</c> module (E12.4): the runtime level and its timed elevation. Elevation is timed only
/// ("Debug for 30 minutes"), capped by a setting, and reverts by itself; there is no permanent elevation
/// from the API. Pushing a level to a device and uploading device logs are the paid feature
/// <c>devices.remote_logging</c> and arrive with the device stories.
/// </summary>
public static class LoggingModule
{
    /// <summary>Route of the level status.</summary>
    public const string Route = "/system/logging";

    /// <summary>Route of the timed elevation.</summary>
    public const string ElevateRoute = "/system/logging/elevate";



    /// <summary>
    /// See the server's log level and start or end a timed elevation.
    /// </summary>
    public static readonly Permission Manage = new("logging.manage", "See the server log level and elevate it for a while") { DefaultRoles = [BuiltInRole.Supervisor, BuiltInRole.Support] };



    /// <summary>
    /// The module descriptor: name <c>logging</c>, the endpoints, the settings, the permission.
    /// </summary>
    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create("logging")
        .WithEndpoints(MapEndpoints)
        .WithSettings(LogLevelSettings.All)
        .WithPermissions(Manage)
        .WithErrorCodes(LoggingErrorCodes.ElevationRejected);



    /// <summary>
    /// Registers the level sync and the module. Call after <c>UseWmsSerilog</c>; a host without it gets a
    /// switch nothing reads.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsLoggingModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<WmsLogLevel>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<LogLevelSync>();
        services.AddHostedService(provider => provider.GetRequiredService<LogLevelSync>());
        services.AddWmsModule(Descriptor);
        return services;
    }



    private static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet(Route, StatusAsync)
            .RequirePermission(Manage)
            .WithName("GetLogLevel")
            .WithSummary("The server's log level: permanent, in force now, and the running elevation (E12.4).")
            .Produces<LoggingStatus>(StatusCodes.Status200OK);
        app.MapPost(ElevateRoute, ElevateAsync)
            .RequirePermission(Manage)
            .WithName("ElevateLogLevel")
            .WithSummary("Lowers the level to Trace, Debug or Information for a number of minutes; reverts by itself.")
            .Produces<LoggingStatus>(StatusCodes.Status200OK);
        app.MapDelete(ElevateRoute, EndElevationAsync)
            .RequirePermission(Manage)
            .WithName("EndLogLevelElevation")
            .WithSummary("Ends the running elevation now.")
            .Produces<LoggingStatus>(StatusCodes.Status200OK);
    }



    private static async Task<IResult> StatusAsync(ISettings settings, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await ReadAsync(settings, timeProvider, cancellationToken).ConfigureAwait(false));
    }



    private static async Task<IResult> ElevateAsync(HttpContext http, ElevateLogLevelRequest body, ISettings settings, TimeProvider timeProvider, LogLevelSync sync, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (!Enum.TryParse<LogLevel>(body.Level, ignoreCase: true, out var level) || level > LogLevel.Information)
        {
            return ApiProblems.Problem(LoggingErrorCodes.ElevationRejected, detail: null, "the level must be Trace, Debug or Information");
        }

        var organization = SettingScopeRef.Organization;
        var max = await settings.GetAsync(LogLevelSettings.MaxElevationMinutes, organization, cancellationToken).ConfigureAwait(false);
        if (body.Minutes < 1 || body.Minutes > max)
        {
            return ApiProblems.Problem(LoggingErrorCodes.ElevationRejected, detail: null, $"minutes must be between 1 and {max}");
        }

        var user = http.User.Identity?.Name ?? SettingsModule.AnonymousUser;
        await settings.SetAsync(LogLevelSettings.ElevatedLevel, organization, level, user, cancellationToken).ConfigureAwait(false);
        await settings.SetAsync(LogLevelSettings.ElevatedUntil, organization, timeProvider.GetUtcNow().AddMinutes(body.Minutes), user, cancellationToken).ConfigureAwait(false);
        await sync.RefreshAsync(cancellationToken).ConfigureAwait(false);   // this instance now; the others within the interval
        return TypedResults.Ok(await ReadAsync(settings, timeProvider, cancellationToken).ConfigureAwait(false));
    }



    private static async Task<IResult> EndElevationAsync(HttpContext http, ISettings settings, TimeProvider timeProvider, LogLevelSync sync, CancellationToken cancellationToken)
    {
        await settings.SetAsync(LogLevelSettings.ElevatedUntil, SettingScopeRef.Organization, DateTimeOffset.UnixEpoch, http.User.Identity?.Name ?? SettingsModule.AnonymousUser, cancellationToken).ConfigureAwait(false);
        await sync.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(await ReadAsync(settings, timeProvider, cancellationToken).ConfigureAwait(false));
    }



    private static async Task<LoggingStatus> ReadAsync(ISettings settings, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var organization = SettingScopeRef.Organization;
        var level = await settings.GetAsync(LogLevelSettings.Level, organization, cancellationToken).ConfigureAwait(false);
        var elevated = await settings.GetAsync(LogLevelSettings.ElevatedLevel, organization, cancellationToken).ConfigureAwait(false);
        var until = await settings.GetAsync(LogLevelSettings.ElevatedUntil, organization, cancellationToken).ConfigureAwait(false);
        var max = await settings.GetAsync(LogLevelSettings.MaxElevationMinutes, organization, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var running = until > now;
        return new LoggingStatus(level, LogLevelSettings.Effective(level, elevated, until, now), running ? elevated : null, running ? until : null, max);
    }
}
