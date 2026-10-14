// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// The built-in <c>local</c> provider (E11.0): accounts in <c>core.user</c>, credentials posted to
/// <c>/auth/local/login</c> (E9.2) and validated through <see cref="ICredentialAuthProvider"/>. Its settings
/// are the lockout keys; its health is whether the account store can be reached now.
/// </summary>
public sealed class LocalAuthProvider : ICredentialAuthProvider
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
    public async Task<AuthProviderHealth> CheckAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var accounts = services.GetRequiredService<ILocalAccounts>();
        if (accounts is NoLocalAccounts)
        {
            return new AuthProviderHealth(Healthy: false, Detail: "The database is not configured; local accounts are unavailable.");
        }

        return await accounts.IsAvailableAsync(cancellationToken).ConfigureAwait(false)
            ? new AuthProviderHealth(Healthy: true, Detail: "Local accounts are stored in the database, which is reachable.")
            : new AuthProviderHealth(Healthy: false, Detail: "The database cannot be reached; local sign-in fails until it is back.");
    }



    /// <inheritdoc/>
    public Task<LocalLoginResult> ValidateAsync(string userName, string password, IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(services);

        return services.GetRequiredService<ILocalAccounts>().LoginAsync(userName, password, cancellationToken);
    }
}
