// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Licensing;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The handlers of the <c>license</c> module (E79.3, E79.6, E79.10, E79.11).
/// </summary>
internal static class LicenseEndpoints
{
    /// <summary>
    /// GET: the license page.
    /// </summary>
    public static async Task<IResult> StatusAsync(HttpContext http, ILicense license, LicenseGate gate, ISettings settings, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await ReadAsync(http, license, gate, settings, cancellationToken).ConfigureAwait(false));
    }



    /// <summary>
    /// PUT: installs a key.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static async Task<IResult> InstallAsync(HttpContext http, InstallLicenseKeyRequest body, LicenseVerifier verifier, LicenseState state, LicenseGate gate, ISettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(state);

        var reading = verifier.Read(body.Key ?? string.Empty);
        if (reading.Key is null || reading.Document is null)
        {
            return ApiProblems.Problem(LicenseErrorCodes.KeyRejected, reading.Reason, reading.Reason ?? "unreadable");
        }

        var stored = await settings.GetAsync(LicenseSettings.Keys, SettingScopeRef.Organization, cancellationToken).ConfigureAwait(false);
        var kept = LicenseState.Lines(stored.Value).Where(line => !string.Equals(verifier.Read(line).Key?.KeyId, reading.Key.KeyId, StringComparison.Ordinal));
        await WriteAsync(http, settings, state, [.. kept, reading.Document], cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(await ReadAsync(http, state, gate, settings, cancellationToken).ConfigureAwait(false));
    }



    /// <summary>
    /// DELETE: removes a key by id.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static async Task<IResult> RemoveAsync(HttpContext http, string keyId, LicenseVerifier verifier, LicenseState state, LicenseGate gate, ISettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(state);

        var stored = await settings.GetAsync(LicenseSettings.Keys, SettingScopeRef.Organization, cancellationToken).ConfigureAwait(false);
        var lines = LicenseState.Lines(stored.Value);
        var kept = lines.Where(line => !string.Equals(verifier.Read(line).Key?.KeyId, keyId, StringComparison.Ordinal)).ToList();
        if (kept.Count == lines.Count)
        {
            return ApiProblems.Problem(LicenseErrorCodes.KeyNotFound, detail: null, keyId);
        }

        await WriteAsync(http, settings, state, kept, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(await ReadAsync(http, state, gate, settings, cancellationToken).ConfigureAwait(false));
    }



    /// <summary>
    /// GET: the comparison with the installed tier marked.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="license"/> is null.</exception>
    public static IResult Comparison(ILicense license)
    {
        ArgumentNullException.ThrowIfNull(license);
        return TypedResults.Ok(FeatureComparisonBuilder.Build(TierTables.Current, license.Current.Tier));
    }



    /// <summary>
    /// The page for the license in force, reconciling every limit's standing on the way.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static async Task<LicenseStatus> ReadAsync(HttpContext http, ILicense license, LicenseGate gate, ISettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(license);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(settings);

        var current = license.Current;
        var warningPercent = await settings.GetAsync(LicenseSettings.UsageWarningPercent, SettingScopeRef.Organization, cancellationToken).ConfigureAwait(false);
        var limits = new List<LimitUsage>();
        foreach (var limit in LicenseLimits.All)
        {
            var decision = await gate.ReconcileAsync(limit, User(http), cancellationToken).ConfigureAwait(false);
            limits.Add(Usage(limit.Description, decision, warningPercent));
        }

        var banners = new List<string>();
        if (current.Coverage == CoverageStatus.Lapsed)
        {
            banners.Add(string.Create(CultureInfo.InvariantCulture, $"The license coverage ended {current.CoveredUntil:yyyy-MM-dd}. Everything in force stays in force; adding sites, devices beyond the current count, raising limits and upgrading need a renewed key."));
        }

        banners.AddRange(limits.Where(l => l.Message.Length > 0).Select(l => l.Message));
        banners.AddRange(limits.Where(l => l is { Warning: true, Message.Length: 0 }).Select(l => string.Create(CultureInfo.InvariantCulture, $"{l.Count} of {l.Ceiling} {l.Description.ToLowerInvariant()} ({l.Percent}%).")));
        return new LicenseStatus(current.Tier.Name, current.Organization, current.Coverage, current.CoveredUntil, ReleaseInfo.Version, ReleaseInfo.ReleaseDate, current.Features.Order(StringComparer.Ordinal).ToList(), limits, current.IncludedDevices, current.PurchasedDevices, current.AllowancePercent, current.AllowanceMinimumUnits, current.GraceDays, warningPercent, current.Keys.Select(Info).ToList(), banners);
    }



    /// <summary>
    /// The page entry for an installed key.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="installed"/> is null.</exception>
    public static InstalledKeyInfo Info(InstalledKey installed)
    {
        ArgumentNullException.ThrowIfNull(installed);

        var key = installed.Key;
        if (key is null)
        {
            return new InstalledKeyInfo(installed.KeyId, Kind: null, Tier: null, Organization: null, Devices: 0, [], CoverageFrom: null, CoverageTo: null, IssuedAt: null, installed.Status, installed.Reason, "unreadable");
        }

        var from = key.IsPerpetual ? (DateOnly?)null : key.Coverage.Min(p => p.From);
        var to = key.IsPerpetual ? (DateOnly?)null : key.Coverage.Max(p => p.To);
        var coverage = to is null ? "perpetual" : string.Create(CultureInfo.InvariantCulture, $"covered to {to:yyyy-MM-dd}");
        var summary = key.Kind == LicenseKeyKind.Base ? $"{key.Tier} base, {coverage}" : string.Join(", ", AddOnParts(key));
        return new InstalledKeyInfo(key.KeyId, key.Kind, key.Tier, key.Organization, key.Devices, key.Features, from, to, key.IssuedAt, installed.Status, installed.Reason, summary);
    }



    private static IEnumerable<string> AddOnParts(LicenseKey key)
    {
        if (key.Devices > 0)
        {
            yield return string.Create(CultureInfo.InvariantCulture, $"+{key.Devices} devices");
        }

        foreach (var feature in key.Features)
        {
            yield return $"+{feature}";
        }

        foreach (var (name, value) in key.Limits)
        {
            yield return string.Create(CultureInfo.InvariantCulture, $"{name} {value}");
        }
    }



    private static LimitUsage Usage(string description, LimitDecision decision, int warningPercent)
    {
        var ceiling = decision.Ceiling.Value;
        var percent = ceiling is { } c && c > 0 ? (int?)(decision.Count * 100 / c) : null;
        return new LimitUsage(decision.Limit.Name, description, ceiling, decision.Count, percent, percent >= warningPercent, decision.Outcome, decision.GraceEndsOn, decision.Message);
    }



    private static async Task WriteAsync(HttpContext http, ISettings settings, LicenseState state, IReadOnlyList<string> lines, CancellationToken cancellationToken)
    {
        await settings.SetAsync(LicenseSettings.Keys, SettingScopeRef.Organization, new SecretText(string.Join('\n', lines)), User(http), cancellationToken).ConfigureAwait(false);
        await state.RefreshAsync(cancellationToken).ConfigureAwait(false);   // this instance now; the others within the sync interval
    }



    private static string User(HttpContext http)
    {
        return http.User.Identity?.Name is { Length: > 0 } name ? name : SettingsModule.AnonymousUser;
    }
}
