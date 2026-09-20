// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Licensing;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The one place limits are enforced (E79.4, E79.5, E79.8): a creation asks <see cref="CheckAsync"/> (or
/// <see cref="RequireAsync"/>, which throws the <c>license.limit_reached</c> problem) with the count
/// recomputed from the data; the decision is the domain's soft-limit rule with the grace period counted
/// from the day the limit first went over, which <see cref="ReconcileAsync"/> records and clears.
/// </summary>
public sealed class LicenseGate
{
    private readonly ILicense _license;
    private readonly ILicenseUsage _usage;
    private readonly ISettings _settings;
    private readonly TimeProvider _timeProvider;



    /// <summary>
    /// Creates the gate.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public LicenseGate(ILicense license, ILicenseUsage usage, ISettings settings, TimeProvider timeProvider)
    {
        _license = license ?? throw new ArgumentNullException(nameof(license));
        _usage = usage ?? throw new ArgumentNullException(nameof(usage));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }



    /// <summary>
    /// The decision for creating <paramref name="adding"/> more of what <paramref name="limit"/> counts.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="limit"/> is null.</exception>
    public async Task<LimitDecision> CheckAsync(LicenseLimit limit, int adding, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(limit);

        var count = await _usage.CountAsync(limit, cancellationToken).ConfigureAwait(false);
        var since = await OverageSinceAsync(limit, cancellationToken).ConfigureAwait(false);
        return LimitCheck.Evaluate(_license.Current, limit, count + adding, since, Today());
    }



    /// <summary>
    /// Lets the creation through or throws the <c>license.limit_reached</c> problem.
    /// </summary>
    /// <exception cref="LicenseException">The creation is blocked.</exception>
    public async Task<LimitDecision> RequireAsync(LicenseLimit limit, int adding, CancellationToken cancellationToken)
    {
        var decision = await CheckAsync(limit, adding, cancellationToken).ConfigureAwait(false);
        return decision.Outcome == LimitOutcome.Blocked ? throw new LicenseException(LicenseErrorCodes.LimitReached, decision.Message) : decision;
    }



    /// <summary>
    /// The current standing of <paramref name="limit"/>, recording the day it first went over and clearing
    /// it once the count is back within the limit (E79.4). Call after a creation and from the license page.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public async Task<LimitDecision> ReconcileAsync(LicenseLimit limit, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(limit);
        ArgumentNullException.ThrowIfNull(updatedBy);

        var count = await _usage.CountAsync(limit, cancellationToken).ConfigureAwait(false);
        var since = await OverageSinceAsync(limit, cancellationToken).ConfigureAwait(false);
        var over = _license.Limit(limit).IsExceededBy(count);
        var today = Today();
        if (over && since is null)
        {
            await _settings.SetAsync(LicenseSettings.OverageSince(limit), SettingScopeRef.Organization, _timeProvider.GetUtcNow(), updatedBy, cancellationToken).ConfigureAwait(false);
            since = today;
        }
        else if (!over && since is not null)
        {
            await _settings.SetAsync(LicenseSettings.OverageSince(limit), SettingScopeRef.Organization, DateTimeOffset.UnixEpoch, updatedBy, cancellationToken).ConfigureAwait(false);
            since = null;
        }

        return LimitCheck.Evaluate(_license.Current, limit, count, since, today);
    }



    private async Task<DateOnly?> OverageSinceAsync(LicenseLimit limit, CancellationToken cancellationToken)
    {
        var since = await _settings.GetAsync(LicenseSettings.OverageSince(limit), SettingScopeRef.Organization, cancellationToken).ConfigureAwait(false);
        return since > DateTimeOffset.UnixEpoch ? DateOnly.FromDateTime(since.UtcDateTime) : null;
    }



    private DateOnly Today()
    {
        return DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
    }
}
