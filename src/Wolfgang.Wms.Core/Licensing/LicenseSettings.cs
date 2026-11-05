// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Licensing;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The licensing settings (E79.3, E79.4, E79.6, E79.9): the installed keys (a secret, encrypted at rest),
/// the usage warning threshold, when each limit first went over (the start of its grace period) and the
/// totes-per-picker setting the license caps.
/// </summary>
public static class LicenseSettings
{
    /// <summary>The installed key documents, one per line; written by the license endpoints, never by hand.</summary>
    public static readonly SettingKey<SecretText> Keys = new("license.keys", new SecretText(string.Empty), "The installed license keys, one signed document per line (stored encrypted). Managed through the license page; the compiled-in free tier needs none.")
    {
        Scopes = SettingScopes.Organization,
    };

    /// <summary>The usage percentage at which a limit is flagged.</summary>
    public static readonly SettingKey<int> UsageWarningPercent = new("license.usage_warning_percent", 80, "The percentage of a limit at which the console warns and an issue is raised.")
    {
        Scopes = SettingScopes.Organization,
        Validator = v => v is >= 1 and <= 100 ? null : "must be between 1 and 100",
    };

    /// <summary>Totes a picker may carry at once; the license's <c>max_totes_per_picker</c> caps it (E79.9).</summary>
    public static readonly SettingKey<int> MaxTotesPerPicker = new("picking.max_totes_per_picker", 1, "Totes a picker may carry at once. The effective value is the lower of this setting and the license's max_totes_per_picker.")
    {
        Scopes = SettingScopes.OrganizationToZone,
        Validator = v => v >= 1 ? null : "must be at least 1",
    };



    private static readonly Dictionary<string, SettingKey<DateTimeOffset>> OverageSinceByLimit = LicenseLimits.All.ToDictionary
    (
        limit => limit.Name,
        limit => new SettingKey<DateTimeOffset>($"license.overage_since.{limit.Name}", DateTimeOffset.UnixEpoch, $"When the {limit.Description.ToLowerInvariant()} count first exceeded the licensed value (UTC); the grace period counts from here. In the past when the count is within the limit.")
        {
            Scopes = SettingScopes.Organization,
        },
        StringComparer.Ordinal
    );



    /// <summary>
    /// Every key, for the module descriptor.
    /// </summary>
    public static IReadOnlyList<SettingKey> All { get; } = [Keys, UsageWarningPercent, MaxTotesPerPicker, .. OverageSinceByLimit.Values];



    /// <summary>
    /// The key recording when <paramref name="limit"/> first went over (E79.4).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="limit"/> is null.</exception>
    public static SettingKey<DateTimeOffset> OverageSince(LicenseLimit limit)
    {
        ArgumentNullException.ThrowIfNull(limit);
        return OverageSinceByLimit[limit.Name];
    }



    /// <summary>
    /// The totes a picker may carry: the lower of the setting and the license (E79.9).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="license"/> is null.</exception>
    public static int EffectiveMaxTotesPerPicker(int setting, EffectiveLicense license)
    {
        ArgumentNullException.ThrowIfNull(license);
        var licensed = license.Limit(LicenseLimits.MaxTotesPerPicker);
        return licensed.IsUnlimited ? setting : Math.Min(setting, licensed.Value!.Value);
    }
}
