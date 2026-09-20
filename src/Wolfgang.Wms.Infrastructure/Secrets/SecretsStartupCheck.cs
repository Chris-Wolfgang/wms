// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Secrets;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Secrets;

/// <summary>
/// Fails startup with a clear message when the connection string is encrypted and cannot be decrypted
/// (E8.1, E8.2): no key ring path, or a ring without the key. Runs before the schema check, which would
/// otherwise report an unreachable database.
/// </summary>
public sealed class SecretsStartupCheck : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly DatabaseOptions _database;
    private readonly KeyRingOptions _keyRing;



    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public SecretsStartupCheck(IServiceProvider services, IOptions<DatabaseOptions> database, IOptions<KeyRingOptions> keyRing)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(keyRing);
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _database = database.Value;
        _keyRing = keyRing.Value;
    }



    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The connection string is encrypted and the key ring cannot decrypt it.</exception>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!ProtectedText.IsProtected(_database.ConnectionString))
        {
            return Task.CompletedTask;
        }

        if (!_keyRing.UsesFileSystem)
        {
            throw new InvalidOperationException($"{DatabaseOptions.SectionName}:ConnectionString is encrypted (enc:v1:) but {KeyRingOptions.PathKey} is not set; an encrypted connection string needs the file key ring that encrypted it, because the database cannot be opened before it is decrypted.");
        }

        _database.EffectiveConnectionString(_services.GetRequiredService<ISecretProtector>());   // throws the clear message when the ring lacks the key
        return Task.CompletedTask;
    }



    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
