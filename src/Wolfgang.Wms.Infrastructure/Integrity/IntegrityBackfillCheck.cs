// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Infrastructure.Secrets;

namespace Wolfgang.Wms.Infrastructure.Integrity;

/// <summary>
/// Creates the integrity key on the first start that finds none, signing every security row as it stands in
/// the same transaction (E10.4: the upgrade path; see <see cref="IntegritySigner.EnsureKeyAsync"/>). Once the
/// key exists a row that fails verification is never re-signed here: repair goes through the application.
/// </summary>
public sealed partial class IntegrityBackfillCheck : IHostedService
{
    private readonly IntegritySigner _signer;
    private readonly IOptions<KeyRingOptions> _keyRing;
    private readonly ILogger<IntegrityBackfillCheck> _logger;



    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public IntegrityBackfillCheck(IntegritySigner signer, IOptions<KeyRingOptions> keyRing, ILogger<IntegrityBackfillCheck> logger)
    {
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _keyRing = keyRing ?? throw new ArgumentNullException(nameof(keyRing));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_keyRing.Value.UsesFileSystem)
        {
            LogRingInDatabase(_logger);   // docfx_project/docs/integrity.md, threat model; #1078 protects the ring itself
        }

        return _signer.EnsureKeyAsync(cancellationToken);   // creates the key and signs the existing rows in one transaction
    }



    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }



    [LoggerMessage(Level = LogLevel.Warning, Message = "Integrity: the Data Protection key ring is stored in the database (wms.data_protection_key) without its own protection, so anyone who can read the database can recover the integrity key and sign changed rows. Signatures still catch accidental and limited-privilege changes; set Wms:DataProtection:KeyRingPath to a directory only the application can read for the full guarantee.")]
    private static partial void LogRingInDatabase(ILogger logger);
}
