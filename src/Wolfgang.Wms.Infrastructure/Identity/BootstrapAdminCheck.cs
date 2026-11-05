// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Identity;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// Creates the bootstrap administrator on first run (E9.1), after the schema check has confirmed the
/// database is ready; a no-op once any local administrator exists, so the bootstrap values are ignored
/// thereafter.
/// </summary>
public sealed class BootstrapAdminCheck : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly BootstrapOptions _options;



    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public BootstrapAdminCheck(IServiceScopeFactory scopes, IOptions<BootstrapOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _options = options.Value;
    }



    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<ILocalAccounts>();
        var userName = string.IsNullOrWhiteSpace(_options.AdminUserName) ? "admin" : _options.AdminUserName;
        await accounts.EnsureBootstrapAdminAsync(userName, cancellationToken).ConfigureAwait(false);
    }



    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
