// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// The built-in <c>local</c> provider (E11.0): accounts in <c>core.user</c>, credentials posted to
/// <c>/auth/local/login</c> (E9.2). Its settings are the lockout keys; its health is whether the stored
/// accounts are available.
/// </summary>
public sealed class LocalAuthProvider : IAuthProvider
{
    /// <inheritdoc/>
    public string Name => AuthProviderSettings.Local;



    /// <inheritdoc/>
    public string DisplayName => "Local account";



    /// <inheritdoc/>
    public AuthProviderKind Kind => AuthProviderKind.Credentials;



    /// <inheritdoc/>
    public IReadOnlyList<SettingKey> Settings { get; } = [AuthSettings.LockoutThreshold, AuthSettings.LockoutDuration];



    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public Task<AuthProviderHealth> CheckAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        return Task.FromResult(services.GetRequiredService<ILocalAccounts>() is NoLocalAccounts
            ? new AuthProviderHealth(Healthy: false, Detail: "The database is not configured; local accounts are unavailable.")
            : new AuthProviderHealth(Healthy: true, Detail: "Local accounts are stored in the database."));
    }
}
