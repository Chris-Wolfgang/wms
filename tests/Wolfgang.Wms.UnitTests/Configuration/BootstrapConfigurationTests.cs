// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Configuration;

namespace Wolfgang.Wms.UnitTests.Configuration;

/// <summary>
/// E6.5: only bootstrap keys are recognised; every other <c>appsettings</c> key is listed with its source,
/// case-insensitively, while environment-style providers are left alone; the startup check logs one warning
/// and never fails.
/// </summary>
public sealed class BootstrapConfigurationTests
{
    private const string Json = """
        {
          "Logging": { "LogLevel": { "Default": "Information" } },
          "allowedhosts": "*",
          "Kestrel": { "Endpoints": { "Https": { "Url": "https://*:8443" } } },
          "Wms": {
            "Database": { "Provider": "None", "Timeout": 30 },
            "DataProtection": { "KeyRingPath": "/keys" },
            "Picking": { "LeaseTimeout": "00:15:00" }
          },
          "Smtp": { "Host": "mail", "Port": 25 },
          "ConnectionStrings": { "Default": "x" }
        }
        """;



    [Fact]
    public void Unrecognised_keys_are_the_leaves_outside_the_bootstrap_set_with_their_source()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Json));
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Anything:FromEnvironment"] = "ignored" })
            .Build();

        var keys = BootstrapConfiguration.UnrecognizedKeys(configuration);

        Assert.Equal
        (
            [
                "ConnectionStrings:Default (appsettings (stream))",
                "Smtp:Host (appsettings (stream))",
                "Smtp:Port (appsettings (stream))",
                "Wms:Database:Timeout (appsettings (stream))",
                "Wms:Picking:LeaseTimeout (appsettings (stream))",
            ],
            keys
        );
    }



    [Fact]
    public void File_providers_report_their_path_and_missing_files_report_nothing()
    {
        var path = Path.Combine(Path.GetTempPath(), "wms-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, """{ "Urls": "http://*:5000", "Extra": 1 }""");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(path, optional: false)
                .AddJsonFile(Path.Combine(Path.GetTempPath(), "wms-missing-" + Guid.NewGuid().ToString("N") + ".json"), optional: true)
                .Build();

            Assert.Equal(["Extra (" + Path.GetFileName(path) + ")"], BootstrapConfiguration.UnrecognizedKeys(configuration));   // the provider reports the path relative to its root
        }
        finally
        {
            File.Delete(path);
        }
    }



    [Theory]
    [InlineData("Logging", true)]
    [InlineData("logging:loglevel:default", true)]
    [InlineData("Kestrel:Endpoints:Http:Url", true)]
    [InlineData("WMS:DATABASE:PROVIDER", true)]
    [InlineData("Wms:Bootstrap:AdminUserName", true)]
    [InlineData("Wms:Database", false)]
    [InlineData("Wms:Database:Password", false)]
    [InlineData("LoggingExtra", false)]
    public void Recognition_is_by_exact_key_or_framework_section(string key, bool expected)
    {
        Assert.Equal(expected, BootstrapConfiguration.IsRecognized(key));
    }



    [Fact]
    public async Task The_check_logs_one_warning_naming_the_keys_and_nothing_when_all_is_well()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""{ "Smtp": { "Host": "mail" }, "Urls": "http://*:5000" }"""));
        var noisy = new ConfigurationBuilder().AddJsonStream(stream).Build();
        var clean = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Smtp:Host"] = "mail" }).Build();
        var log = new ListLogger();

        await new BootstrapConfigurationCheck(noisy, log).StartAsync(CancellationToken.None);
        await new BootstrapConfigurationCheck(clean, log).StartAsync(CancellationToken.None);
        await new BootstrapConfigurationCheck(noisy.GetSection("Smtp"), log).StartAsync(CancellationToken.None);   // not a root: skipped
        await new BootstrapConfigurationCheck(noisy, log).StopAsync(CancellationToken.None);

        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("1 unrecognised key(s)", entry.Message, StringComparison.Ordinal);
        Assert.Contains("Smtp:Host (appsettings (stream))", entry.Message, StringComparison.Ordinal);
        Assert.Null(log.BeginScope("scope"));
        Assert.Throws<ArgumentNullException>(() => new BootstrapConfigurationCheck(null!, log));
        Assert.Throws<ArgumentNullException>(() => new BootstrapConfigurationCheck(noisy, null!));
        Assert.Throws<ArgumentNullException>(() => BootstrapConfiguration.UnrecognizedKeys(null!));
        Assert.Throws<ArgumentNullException>(() => BootstrapConfiguration.IsRecognized(null!));
    }



    [Fact]
    public void Registration_adds_the_hosted_check()
    {
        var services = new ServiceCollection();

        services.AddWmsBootstrapConfigurationCheck();

        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(BootstrapConfigurationCheck));
        Assert.Throws<ArgumentNullException>(() => BootstrapConfiguration.AddWmsBootstrapConfigurationCheck(null!));
    }



    private sealed class ListLogger : ILogger<BootstrapConfigurationCheck>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
