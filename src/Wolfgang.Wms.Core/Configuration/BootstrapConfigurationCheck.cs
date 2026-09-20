// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wolfgang.Wms.Core.Configuration;

/// <summary>
/// At startup, names every <c>appsettings</c> key the product does not recognise (E6.5) in one warning and
/// carries on: the key is ignored, and the operator learns where the value belongs.
/// </summary>
public sealed partial class BootstrapConfigurationCheck : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BootstrapConfigurationCheck> _logger;



    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public BootstrapConfigurationCheck(IConfiguration configuration, ILogger<BootstrapConfigurationCheck> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_configuration is IConfigurationRoot root)
        {
            var keys = BootstrapConfiguration.UnrecognizedKeys(root);
            if (keys.Count > 0)
            {
                LogUnrecognized(_logger, keys.Count, string.Join(", ", keys));
            }
        }

        return Task.CompletedTask;
    }



    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }



    [LoggerMessage(Level = LogLevel.Warning, Message = "appsettings holds {Count} unrecognised key(s), ignored: {Keys}. Only bootstrap keys belong there (docs/CONFIGURATION.md); everything else is a setting in the Configure workspace.")]
    private static partial void LogUnrecognized(ILogger logger, int count, string keys);
}
