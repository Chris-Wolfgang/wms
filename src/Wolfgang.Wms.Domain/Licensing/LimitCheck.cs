// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// The soft-limit rule (E79.4, E79.5): a creation that stays under the ceiling is allowed; one that goes
/// over is allowed within the allowance until the grace period (counted from the first day over) ends,
/// with a banner; beyond the allowance, after grace, or while coverage has lapsed it is blocked. Nothing
/// already created ever stops working; dropping back under the limit clears the state.
/// </summary>
public static class LimitCheck
{
    /// <summary>
    /// Decides a creation that would bring <paramref name="limit"/>'s count to <paramref name="countAfter"/>.
    /// </summary>
    /// <param name="license">The effective license.</param>
    /// <param name="limit">The limit.</param>
    /// <param name="countAfter">The count once the creation happened.</param>
    /// <param name="overageSince">The day the count first exceeded the ceiling, when it still does; null otherwise.</param>
    /// <param name="today">Today.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static LimitDecision Evaluate(EffectiveLicense license, LicenseLimit limit, int countAfter, DateOnly? overageSince, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(license);
        ArgumentNullException.ThrowIfNull(limit);

        var ceiling = license.Limit(limit);
        if (!ceiling.IsExceededBy(countAfter))
        {
            return new LimitDecision(LimitOutcome.Allowed, limit, ceiling, countAfter, GraceEndsOn: null, string.Empty);
        }

        var description = limit.Description.ToLowerInvariant();
        if (license.Coverage == CoverageStatus.Lapsed)
        {
            return new LimitDecision(LimitOutcome.Blocked, limit, ceiling, countAfter, GraceEndsOn: null, string.Create(CultureInfo.InvariantCulture, $"The license coverage ended {license.CoveredUntil:yyyy-MM-dd}: no more {description} can be added on the {license.Tier.Name} tier until it is renewed."));
        }

        var allowed = ceiling.Value!.Value + license.AllowanceUnits(limit);
        var graceEnds = (overageSince ?? today).AddDays(license.GraceDays);
        if (countAfter > allowed)
        {
            return new LimitDecision(LimitOutcome.Blocked, limit, ceiling, countAfter, graceEnds, string.Create(CultureInfo.InvariantCulture, $"{countAfter} of {ceiling} {description}: the {license.Tier.Name} tier allows at most {allowed} while licenses are added; install a larger key or remove one."));
        }

        if (today > graceEnds)
        {
            return new LimitDecision(LimitOutcome.Blocked, limit, ceiling, countAfter, graceEnds, string.Create(CultureInfo.InvariantCulture, $"{countAfter} of {ceiling} {description}: the grace period ended {graceEnds:yyyy-MM-dd}; add licenses to the {license.Tier.Name} tier or remove one."));
        }

        var daysLeft = graceEnds.DayNumber - today.DayNumber;
        return new LimitDecision(LimitOutcome.WithinAllowance, limit, ceiling, countAfter, graceEnds, string.Create(CultureInfo.InvariantCulture, $"{countAfter} of {ceiling} {description} — {daysLeft} days to add licenses."));
    }
}
