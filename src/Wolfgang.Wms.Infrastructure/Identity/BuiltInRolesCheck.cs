// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolfgang.Wms.Core.Authorization;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// On every start, after the schema check, creates the built-in roles that are missing and refreshes their
/// permission sets from the catalog (E10.2), so a new module's permissions reach the right roles without a
/// migration.
/// </summary>
public sealed class BuiltInRolesCheck : IHostedService
{
    private readonly IServiceScopeFactory _scopes;



    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="scopes"/> is null.</exception>
    public BuiltInRolesCheck(IServiceScopeFactory scopes)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
    }



    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<IRoles>();
        var catalog = scope.ServiceProvider.GetRequiredService<PermissionCatalog>();
        await roles.EnsureBuiltInAsync(catalog, cancellationToken).ConfigureAwait(false);
    }



    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
