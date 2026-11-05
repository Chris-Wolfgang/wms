// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Licensing;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The license in force on this instance (E79.3, E79.11): the installed key documents read from the
/// <c>license.keys</c> setting, each verified by the <see cref="LicenseVerifier"/>, composed with the
/// release's tier table. Until the first refresh, and on a host without a settings store, it is the free
/// tier. A document that fails verification is kept in the list as invalid, so the license page shows it.
/// </summary>
public sealed partial class LicenseState : ILicense
{
    private readonly LicenseVerifier _verifier;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LicenseState> _logger;
    private volatile EffectiveLicense _current = Compose([]);



    /// <summary>
    /// Creates the state.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public LicenseState(LicenseVerifier verifier, IServiceScopeFactory scopes, ILogger<LicenseState> logger)
    {
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <inheritdoc/>
    public EffectiveLicense Current => _current;



    /// <inheritdoc/>
    public bool HasFeature(LicenseFeature feature)
    {
        return _current.HasFeature(feature);
    }



    /// <inheritdoc/>
    public LimitValue Limit(LicenseLimit limit)
    {
        return _current.Limit(limit);
    }



    /// <inheritdoc/>
    public async Task<EffectiveLicense> RefreshAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettings>();
        var keys = await settings.GetAsync(LicenseSettings.Keys, SettingScopeRef.Organization, cancellationToken).ConfigureAwait(false);
        var previous = _current;
        var next = Compose(Read(keys.Value));
        _current = next;
        if (!string.Equals(previous.Tier.Name, next.Tier.Name, StringComparison.Ordinal) || previous.Coverage != next.Coverage || previous.Keys.Count != next.Keys.Count)
        {
            LogChanged(_logger, next.Tier.Name, next.Coverage, next.Keys.Count(k => k.Status == KeyStatus.Active), next.Keys.Count);
        }

        return next;
    }



    /// <summary>
    /// Every installed key of <paramref name="documents"/> (the stored text, one document per line) with
    /// the verifier's verdict; the composer assigns the final statuses.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="documents"/> is null.</exception>
    public IReadOnlyList<InstalledKey> Read(string documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var keys = new List<InstalledKey>();
        foreach (var (line, index) in Lines(documents).Select((l, i) => (l, i)))
        {
            var reading = _verifier.Read(line);
            keys.Add(reading.Key is null
                ? new InstalledKey(Key: null, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"line {index + 1}"), KeyStatus.Invalid, reading.Reason)
                : new InstalledKey(reading.Key, reading.Key.KeyId, KeyStatus.Active, Reason: null));
        }

        return keys;
    }



    /// <summary>
    /// The license for a set of installed keys on this release.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="installed"/> is null.</exception>
    public static EffectiveLicense Compose(IReadOnlyList<InstalledKey> installed)
    {
        return LicenseComposer.Compose(TierTables.Current, installed, ReleaseInfo.ReleaseDate);
    }



    /// <summary>
    /// The non-blank lines of the stored text.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="documents"/> is null.</exception>
    public static IReadOnlyList<string> Lines(string documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        return documents.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
    }



    [LoggerMessage(Level = LogLevel.Information, Message = "License is now the {Tier} tier ({Coverage}) with {ActiveKeys} of {InstalledKeys} installed keys active.")]
    private static partial void LogChanged(ILogger logger, string tier, CoverageStatus coverage, int activeKeys, int installedKeys);
}
