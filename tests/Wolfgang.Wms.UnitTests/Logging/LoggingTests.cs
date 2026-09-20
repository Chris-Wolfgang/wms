// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Logging;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;
using Wolfgang.Wms.Logging;
using MsLogger = Microsoft.Extensions.Logging.ILogger;

namespace Wolfgang.Wms.UnitTests.Logging;

/// <summary>
/// E12.2–E12.4 without a host: redaction by name and inside text, the Verbose/SQL rate limit, level names
/// and the switch, the options and the sinks they select (an in-memory sink sees redacted, level-filtered
/// events), the correlation properties of a request, the effective level and the settings-driven sync.
/// </summary>
public sealed class LoggingTests
{
    [Theory]
    [InlineData("Password", true)]
    [InlineData("ConnectionString", true)]
    [InlineData("client_secret", true)]
    [InlineData("Api-Key", true)]
    [InlineData("LicenseKey", true)]
    [InlineData("Pin", true)]
    [InlineData("Authorization", true)]
    [InlineData("UserName", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Sensitive_property_names_are_recognised(string? name, bool sensitive)
    {
        Assert.Equal(sensitive, RedactingEnricher.IsSensitiveName(name));
    }



    [Fact]
    public void Secrets_inside_text_are_masked()
    {
        Assert.Equal("value enc:v1:*** end", RedactingEnricher.MaskText("value enc:v1:CfDJ8abc+/=x end"));
        Assert.Equal("Server=x;Password=***;Database=y", RedactingEnricher.MaskText("Server=x;Password=s3cr3t!;Database=y"));
        Assert.Equal("Host=x;pwd=***", RedactingEnricher.MaskText("Host=x;pwd=abc"));
        Assert.Equal("Authorization: Bearer ***", RedactingEnricher.MaskText("Authorization: Bearer eyJhbGciOi.abc-def_ghi=="));
        Assert.Equal("nothing here", RedactingEnricher.MaskText("nothing here"));
        Assert.Throws<ArgumentNullException>(() => RedactingEnricher.MaskText(null!));
    }



    [Fact]
    public void Events_are_redacted_and_the_switch_filters_them()
    {
        var sink = new MemorySink();
        var level = new WmsLogLevel(LogEventLevel.Information);
        var options = new LoggingOptions { Stdout = false };
        using var logger = WmsLogging.Configure(new LoggerConfiguration(), options, level, TimeProvider.System, isWindows: false).WriteTo.Sink(sink).CreateLogger();

        logger.Information("Connecting with {ConnectionString} as {User} token {Note}", "Server=x;Password=p;", "alice", "Bearer abc.def");
        logger.Debug("hidden");
        level.Apply(LogEventLevel.Debug);
        logger.Debug("shown");

        var connecting = sink.Events.Single(e => e.MessageTemplate.Text.StartsWith("Connecting", StringComparison.Ordinal));
        Assert.Equal("***", ((ScalarValue)connecting.Properties["ConnectionString"]).Value);
        Assert.Equal("alice", ((ScalarValue)connecting.Properties["User"]).Value);
        Assert.Equal("Bearer ***", ((ScalarValue)connecting.Properties["Note"]).Value);
        Assert.Equal(["Connecting with {ConnectionString} as {User} token {Note}", "shown"], sink.Events.Select(e => e.MessageTemplate.Text));
        Assert.Equal(LogEventLevel.Debug, level.Current);
        Assert.False(level.Apply(LogEventLevel.Debug));
    }



    [Fact]
    public void Verbose_and_sql_events_are_rate_limited()
    {
        var filter = new VerboseRateLimit(2, TimeProvider.System);
        var verbose = Event(LogEventLevel.Verbose, sourceContext: null);
        var sql = Event(LogEventLevel.Information, VerboseRateLimit.SqlSourceContext);
        var information = Event(LogEventLevel.Information, sourceContext: "App");

        Assert.True(filter.IsEnabled(verbose));
        Assert.True(filter.IsEnabled(sql));
        Assert.False(filter.IsEnabled(verbose));
        Assert.True(filter.IsEnabled(information));
        Assert.Equal(1, filter.Dropped);
        Assert.Throws<ArgumentOutOfRangeException>(() => new VerboseRateLimit(0, TimeProvider.System));
        Assert.Throws<ArgumentNullException>(() => new VerboseRateLimit(1, null!));
        Assert.Throws<ArgumentNullException>(() => filter.IsEnabled(null!));
        Assert.Throws<ArgumentNullException>(() => new RedactingEnricher().Enrich(null!, new StubFactory()));
        Assert.Throws<ArgumentNullException>(() => new RedactingEnricher().Enrich(verbose, null!));
    }



    [Theory]
    [InlineData("Verbose", LogEventLevel.Verbose)]
    [InlineData("trace", LogEventLevel.Verbose)]
    [InlineData("Debug", LogEventLevel.Debug)]
    [InlineData("Warning", LogEventLevel.Warning)]
    [InlineData("Critical", LogEventLevel.Fatal)]
    [InlineData("Fatal", LogEventLevel.Fatal)]
    [InlineData("nonsense", LogEventLevel.Information)]
    [InlineData(null, LogEventLevel.Information)]
    public void Level_names_parse(string? name, LogEventLevel expected)
    {
        Assert.Equal(expected, WmsLogLevel.Parse(name));
        Assert.Equal(LogEventLevel.Fatal, WmsLogLevel.ToSerilog(LogLevel.None));
    }



    [Fact]
    public void Options_select_the_sinks()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Wms:Logging:Level"] = "Debug",
            ["Wms:Logging:File:Path"] = Path.Combine(Path.GetTempPath(), "wms-tests", "wms-.log"),
            ["Wms:Logging:File:RetainedDays"] = "3",
            ["Wms:Logging:EventLog:Source"] = "Wolfgang.Wms",
            ["Wms:Logging:OpenTelemetry:Endpoint"] = "http://127.0.0.1:4318",
            ["Wms:Logging:OpenTelemetry:Protocol"] = "http",
            ["Wms:Logging:VerbosePerSecond"] = "50",
        }).Build();

        var options = WmsLogging.Read(configuration);
        var defaults = WmsLogging.Read(new ConfigurationBuilder().Build());

        Assert.Equal(("Debug", 3, "Wolfgang.Wms", "http://127.0.0.1:4318", "http", 50, false), (options.Level, options.FileRetainedDays, options.EventLogSource, options.OpenTelemetryEndpoint, options.OpenTelemetryProtocol, options.VerbosePerSecond, options.WritesToStdout));
        Assert.True(defaults.WritesToStdout);
        Assert.Equal("Information", defaults.Level);
        using var logger = WmsLogging.Configure(new LoggerConfiguration(), options, new WmsLogLevel(), TimeProvider.System, isWindows: false).CreateLogger();
        using var windows = WmsLogging.Configure(new LoggerConfiguration(), new LoggingOptions { EventLogSource = "Wolfgang.Wms", Stdout = false }, new WmsLogLevel(), TimeProvider.System, isWindows: OperatingSystem.IsWindows()).CreateLogger();
        using var stdout = WmsLogging.Configure(new LoggerConfiguration(), new LoggingOptions(), new WmsLogLevel(), TimeProvider.System, isWindows: false).CreateLogger();
        logger.Information("to the file");
        Assert.Throws<ArgumentNullException>(() => WmsLogging.Read(null!));
        Assert.Throws<ArgumentNullException>(() => WmsLogging.Configure(null!, options, new WmsLogLevel(), TimeProvider.System, isWindows: false));
        Assert.Throws<ArgumentNullException>(() => WmsLogging.Configure(new LoggerConfiguration(), null!, new WmsLogLevel(), TimeProvider.System, isWindows: false));
        Assert.Throws<ArgumentNullException>(() => WmsLogging.Configure(new LoggerConfiguration(), options, null!, TimeProvider.System, isWindows: false));
        Assert.Throws<ArgumentNullException>(() => WmsLogging.Configure(new LoggerConfiguration(), options, new WmsLogLevel(), null!, isWindows: false));
        Assert.Throws<ArgumentNullException>(() => WmsLogging.UseWmsSerilog(null!));
    }



    [Fact]
    public void A_host_builder_gets_serilog_and_the_switch()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Wms:Logging:Level"] = "Warning", ["Wms:Logging:Stdout"] = "false" });

        builder.UseWmsSerilog();
        using var host = builder.Build();

        Assert.Equal(LogEventLevel.Warning, host.Services.GetRequiredService<WmsLogLevel>().Current);
        Assert.False(host.Services.GetRequiredService<LoggingOptions>().WritesToStdout);
        Assert.NotNull(host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("x"));
    }



    [Fact]
    public void Correlation_properties_come_from_the_request()
    {
        var context = new DefaultHttpContext();
        context.User = SessionClaims.Principal(new LocalUser(7, "alice", "Alice", MustChangePassword: false, IsLocalAdmin: false, IsDisabled: false, Grants: []));
        context.Request.Headers[WmsCorrelation.DeviceHeader] = "HH-42";
        context.Features.Set<IRouteValuesFeature>(new RouteValuesFeature { RouteValues = new RouteValueDictionary { ["toteId"] = "T-1", ["siteId"] = 3L, ["other"] = "x" } });
        var anonymous = new DefaultHttpContext();

        var properties = WmsCorrelation.Properties(context);
        var bare = WmsCorrelation.Properties(anonymous);

        Assert.Equal(7L, properties["UserId"]);
        Assert.Equal("alice", properties["User"]);
        Assert.Equal("HH-42", properties["Device"]);
        Assert.Equal("T-1", properties["ToteId"]);
        Assert.Equal(3L, properties["SiteId"]);
        Assert.False(properties.ContainsKey("Other"));
        Assert.NotNull(properties["TraceId"]);
        Assert.Equal(["TraceId"], bare.Keys);
        Assert.Throws<ArgumentNullException>(() => WmsCorrelation.Properties(null!));
        Assert.Throws<ArgumentNullException>(() => WmsCorrelation.UseWmsCorrelation(null!));
    }



    [Fact]
    public async Task The_correlation_middleware_scopes_the_request()
    {
        var provider = new ScopeCapture();
        var services = new ServiceCollection().AddLogging(logging => logging.AddProvider(provider)).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Headers[WmsCorrelation.DeviceHeader] = "HH-1";
        var app = new ApplicationBuilder(services);
        app.UseWmsCorrelation();
        app.Run(http => { http.RequestServices.GetRequiredService<ILogger<LoggingTests>>().LogInformation("inside"); return Task.CompletedTask; });
        var bare = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        bare.UseWmsCorrelation();
        var reached = false;
        bare.Run(_ => { reached = true; return Task.CompletedTask; });

        await app.Build()(context);
        await bare.Build()(new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() });

        Assert.Equal("HH-1", provider.Scopes.Single()["Device"]);
        Assert.True(reached);   // no logger factory: the request still runs
    }



    [Fact]
    public async Task The_sync_applies_the_effective_level_from_the_settings()
    {
        var settings = new FakeSettings();
        var level = new WmsLogLevel(LogEventLevel.Information);
        using var provider = new ServiceCollection().AddLogging().AddScoped<ISettings>(_ => settings).BuildServiceProvider();
        var time = TimeProvider.System;
        using var sync = new LogLevelSync(level, provider.GetRequiredService<IServiceScopeFactory>(), time, new StartedLifetime(), NullLogger<LogLevelSync>.Instance);

        Assert.False(await sync.RefreshAsync(CancellationToken.None));   // Information already
        settings.Values["logging.elevated_level"] = "Debug";
        settings.Values["logging.elevated_until"] = time.GetUtcNow().AddMinutes(5).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(await sync.RefreshAsync(CancellationToken.None));
        Assert.Equal(LogEventLevel.Debug, level.Current);
        settings.Values["logging.elevated_until"] = time.GetUtcNow().AddMinutes(-5).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        settings.Values["logging.level"] = "Warning";
        Assert.True(await sync.RefreshAsync(CancellationToken.None));
        Assert.Equal(LogEventLevel.Warning, level.Current);

        await sync.StartAsync(CancellationToken.None);   // started lifetime: refreshes at once, then ticks
        await sync.StopAsync(CancellationToken.None);
        Assert.Equal(LogLevel.Debug, LogLevelSettings.Effective(LogLevel.Warning, LogLevel.Debug, DateTimeOffset.MaxValue, DateTimeOffset.UtcNow));
        Assert.Equal(LogLevel.Warning, LogLevelSettings.Effective(LogLevel.Warning, LogLevel.Debug, DateTimeOffset.UnixEpoch, DateTimeOffset.UtcNow));
        Assert.NotNull(LogLevelSettings.ElevatedLevel.Validate(LogLevel.Warning));
        Assert.Null(LogLevelSettings.ElevatedLevel.Validate(LogLevel.Trace));
        Assert.NotNull(LogLevelSettings.MaxElevationMinutes.Validate(0));
        Assert.Null(LogLevelSettings.Level.Validate(LogLevel.Error));
        Assert.NotNull(LogLevelSettings.Level.Validate(LogLevel.None));
        Assert.Throws<ArgumentNullException>(() => new LogLevelSync(null!, provider.GetRequiredService<IServiceScopeFactory>(), time, new StartedLifetime(), NullLogger<LogLevelSync>.Instance));
        Assert.Throws<ArgumentNullException>(() => new LogLevelSync(level, null!, time, new StartedLifetime(), NullLogger<LogLevelSync>.Instance));
        Assert.Throws<ArgumentNullException>(() => new LogLevelSync(level, provider.GetRequiredService<IServiceScopeFactory>(), null!, new StartedLifetime(), NullLogger<LogLevelSync>.Instance));
        Assert.Throws<ArgumentNullException>(() => new LogLevelSync(level, provider.GetRequiredService<IServiceScopeFactory>(), time, null!, NullLogger<LogLevelSync>.Instance));
        Assert.Throws<ArgumentNullException>(() => new LogLevelSync(level, provider.GetRequiredService<IServiceScopeFactory>(), time, new StartedLifetime(), null!));
    }



    [Fact]
    public void The_module_registers_its_settings_and_permission()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostApplicationLifetime>(new StartedLifetime());

        services.AddWmsLoggingModule();
        using var provider = services.BuildServiceProvider();

        var module = provider.GetRequiredService<ModuleCollection>().Modules.Single(m => string.Equals(m.Name, "logging", StringComparison.Ordinal));
        Assert.Equal(["logging.elevated_level", "logging.elevated_until", "logging.elevation_max_minutes", "logging.level"], module.Settings.Select(s => s.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["logging.manage"], module.Permissions.Select(p => p.Name));
        Assert.NotNull(provider.GetRequiredService<WmsLogLevel>());
        Assert.NotNull(provider.GetRequiredService<LogLevelSync>());
        Assert.Throws<ArgumentNullException>(() => LoggingModule.AddWmsLoggingModule(null!));
        Assert.Equal(400, LoggingErrorCodes.ElevationRejected.HttpStatus);
    }



    [Fact]
    public async Task The_test_doubles_behave()
    {
        var factory = new StubFactory();
        var capture = new ScopeCapture();
        var lifetime = new StartedLifetime();
        var settings = new FakeSettings();
        var scope = SettingScopeRef.Organization;

        Assert.Equal("v", ((ScalarValue)factory.CreateProperty("n", "v").Value).Value);
        Assert.True(capture.IsEnabled(LogLevel.Trace));
        capture.Log(LogLevel.Information, new EventId(1), "state", exception: null, (s, _) => s);
        Assert.Null(capture.BeginScope("not a dictionary"));
        capture.Dispose();
        lifetime.StopApplication();
        Assert.False(lifetime.ApplicationStopping.IsCancellationRequested);
        Assert.False(lifetime.ApplicationStopped.IsCancellationRequested);
        Assert.True(lifetime.ApplicationStarted.IsCancellationRequested);
        Assert.Equal("logging.level", (await settings.SetAsync(LogLevelSettings.Level, scope, LogLevel.Warning, "me", CancellationToken.None)).Name);
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.ResetAsync(LogLevelSettings.Level, scope, "me", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.SetModeAsync(LogLevelSettings.Level, scope, CascadeMode.Value, "me", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.PopulateAsync(scope, "me", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.ListAsync(scope, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.SetTextAsync("logging.level", scope, "x", "me", CancellationToken.None));
    }



    private static LogEvent Event(LogEventLevel level, string? sourceContext)
    {
        var properties = sourceContext is null ? [] : new List<LogEventProperty> { new("SourceContext", new ScalarValue(sourceContext)) };
        return new LogEvent(DateTimeOffset.UtcNow, level, exception: null, new MessageTemplateParser().Parse("x"), properties);
    }



    private sealed class MemorySink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }



    private sealed class StubFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }



    private sealed class ScopeCapture : ILoggerProvider, MsLogger
    {
        public List<Dictionary<string, object>> Scopes { get; } = [];

        public MsLogger CreateLogger(string categoryName) => this;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            if (state is Dictionary<string, object> scope)
            {
                Scopes.Add(scope);
            }

            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        public void Dispose()
        {
        }
    }



    private sealed class StartedLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => new(canceled: true);

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }



    private sealed class FakeSettings : ISettings
    {
        private readonly DefaultSettings _defaults = new(new SettingRegistry(LogLevelSettings.All));

        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

        public async Task<T> GetAsync<T>(SettingKey<T> key, SettingScopeRef scope, CancellationToken cancellationToken)
        {
            return Values.TryGetValue(key.Name, out var text) && key.Codec.TryParse(text, out var value) ? value : await _defaults.GetAsync(key, scope, cancellationToken);
        }

        public Task<SettingValue> FindAsync(string name, SettingScopeRef scope, CancellationToken cancellationToken) => _defaults.FindAsync(name, scope, cancellationToken);

        public Task<SettingValue> SetAsync<T>(SettingKey<T> key, SettingScopeRef scope, T value, string updatedBy, CancellationToken cancellationToken)
        {
            Values[key.Name] = key.Codec.Format(value);
            return FindAsync(key.Name, scope, cancellationToken);
        }

        public Task<SettingValue> ResetAsync(SettingKey key, SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SettingValue> SetModeAsync(SettingKey key, SettingScopeRef scope, CascadeMode mode, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> PopulateAsync(SettingScopeRef scope, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<SettingValue>> ListAsync(SettingScopeRef scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SettingValue> SetTextAsync(string name, SettingScopeRef scope, string? text, string updatedBy, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
