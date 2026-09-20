// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E6.5 on the real API host: an <c>appsettings</c> key outside the bootstrap set is named in one startup
/// warning; the shipped <c>appsettings.json</c> raises none.
/// </summary>
public sealed class BootstrapConfigurationWarningTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;



    public BootstrapConfigurationWarningTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public void An_unrecognised_appsettings_key_is_warned_about_at_startup()
    {
        var sink = new WarningSink();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""{ "Smtp": { "Host": "mail" } }"""));
        using var host = _factory.WithWebHostBuilder(builder => builder
            .ConfigureAppConfiguration(configuration => configuration.AddJsonStream(stream))
            .ConfigureLogging(logging => logging.AddProvider(sink)));
        using var client = host.CreateClient();

        var warning = Assert.Single(sink.Warnings, w => w.Contains("unrecognised key", StringComparison.Ordinal));
        Assert.Contains("Smtp:Host (appsettings (stream))", warning, StringComparison.Ordinal);
    }



    [Fact]
    public void The_shipped_appsettings_raise_no_warning()
    {
        var sink = new WarningSink();
        using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(sink)));
        using var client = host.CreateClient();

        Assert.DoesNotContain(sink.Warnings, w => w.Contains("unrecognised key", StringComparison.Ordinal));
        Assert.Null(sink.BeginScope("scope"));
        Assert.False(sink.IsEnabled(LogLevel.Information));
        sink.Dispose();
    }



    private sealed class WarningSink : ILoggerProvider, ILogger
    {
        public ConcurrentQueue<string> Warnings { get; } = new();

        public ILogger CreateLogger(string categoryName)
        {
            return this;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Warning;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                Warnings.Enqueue(formatter(state, exception));
            }
        }

        public void Dispose()
        {
        }
    }
}
