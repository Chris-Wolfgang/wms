// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;

namespace Wolfgang.Wms.Logging;

/// <summary>
/// The hosts' logging (E12.2): Serilog with plain JSON lines (compact format) to standard output in a
/// container, a rolling file and the Windows Event Log for a service or IIS install, and the OpenTelemetry
/// sink for every other destination through the customer's collector. No database sink: application logs
/// stay out of the business database. Every sink is asynchronous, every event is redacted, the chatty
/// levels are rate-limited, and the minimum level is a switch the settings move at runtime (E12.4).
/// </summary>
public static class WmsLogging
{
    /// <summary>
    /// Replaces the framework loggers with Serilog built from <c>Wms:Logging</c>, and registers the level
    /// switch for the settings-driven sync.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static IHostApplicationBuilder UseWmsSerilog(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = Read(builder.Configuration);
        var level = new WmsLogLevel(WmsLogLevel.Parse(options.Level));
        var providers = new LoggerProviderCollection();   // ILoggerProviders registered afterwards (tests, a platform's own) still receive every event
        var logger = Configure(new LoggerConfiguration(), options, level, TimeProvider.System, OperatingSystem.IsWindows()).WriteTo.Providers(providers).CreateLogger();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(level);
        builder.Services.AddSingleton(options);
        builder.Services.AddSerilog(logger, dispose: true, providers);
        return builder;
    }



    /// <summary>
    /// The options bound from <see cref="LoggingOptions.SectionName"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
    public static LoggingOptions Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(LoggingOptions.SectionName);
        var options = new LoggingOptions
        {
            Level = section["Level"] ?? "Information",
            FilePath = Blank(section["File:Path"]),
            EventLogSource = Blank(section["EventLog:Source"]),
            OpenTelemetryEndpoint = Blank(section["OpenTelemetry:Endpoint"]),
            OpenTelemetryProtocol = section["OpenTelemetry:Protocol"] ?? "grpc",
        };
        if (bool.TryParse(section["Stdout"], out var stdout))
        {
            options.Stdout = stdout;
        }

        if (int.TryParse(section["File:RetainedDays"], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var days) && days > 0)
        {
            options.FileRetainedDays = days;
        }

        if (int.TryParse(section["VerbosePerSecond"], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var perSecond) && perSecond > 0)
        {
            options.VerbosePerSecond = perSecond;
        }

        return options;
    }



    /// <summary>
    /// Applies the level switch, the enrichers, the rate limit and the sinks <paramref name="options"/>
    /// name to <paramref name="configuration"/>; tests add their own sink afterwards.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static LoggerConfiguration Configure(LoggerConfiguration configuration, LoggingOptions options, WmsLogLevel level, TimeProvider timeProvider, bool isWindows)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(timeProvider);

        configuration
            .MinimumLevel.ControlledBy(level.Switch)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.With(new RedactingEnricher())
            .Filter.With(new VerboseRateLimit(options.VerbosePerSecond, timeProvider));

        if (options.WritesToStdout)
        {
            configuration.WriteTo.Async(sink => sink.Console(new CompactJsonFormatter()));
        }

        if (options.FilePath is { } path)
        {
            configuration.WriteTo.Async(sink => sink.File(new CompactJsonFormatter(), path, rollingInterval: RollingInterval.Day, retainedFileCountLimit: options.FileRetainedDays, shared: true));
        }

        if (options.EventLogSource is { } source && isWindows)
        {
            configuration.WriteTo.Async(sink => sink.EventLog(source, restrictedToMinimumLevel: LogEventLevel.Warning));   // the Event Log is for what an operator must see
        }

        if (options.OpenTelemetryEndpoint is { } endpoint)
        {
            var protocol = string.Equals(options.OpenTelemetryProtocol, "http", StringComparison.OrdinalIgnoreCase) ? OtlpProtocol.HttpProtobuf : OtlpProtocol.Grpc;
            configuration.WriteTo.OpenTelemetry(otlp =>
            {
                otlp.Endpoint = endpoint;
                otlp.Protocol = protocol;
                otlp.ResourceAttributes = new Dictionary<string, object>(StringComparer.Ordinal) { ["service.name"] = "wolfgang-wms" };
            });
        }

        return configuration;
    }



    private static string? Blank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
