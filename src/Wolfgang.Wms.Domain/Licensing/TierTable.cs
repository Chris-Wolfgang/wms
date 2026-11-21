// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// A release's tier table (E79.1): tier → features and limits. Complete by construction (every limit valued
/// in every tier) and monotonic across releases (a feature never moves to a higher tier, a limit never goes
/// down, a tier is never removed): <see cref="Verify"/> and <see cref="Regressions"/> are what the build
/// and the CI check run.
/// </summary>
public sealed class TierTable
{
    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="version"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="tiers"/> is null.</exception>
    public TierTable(string version, IReadOnlyList<TierDefinition> tiers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        Version = version;
        Tiers = tiers ?? throw new ArgumentNullException(nameof(tiers));
    }



    /// <summary>The release the table belongs to.</summary>
    public string Version { get; }

    /// <summary>The tiers, lowest first.</summary>
    public IReadOnlyList<TierDefinition> Tiers { get; }



    /// <summary>
    /// The definition of a tier, or null when the table has none for it.
    /// </summary>
    public TierDefinition? Find(LicenseTier? tier)
    {
        return tier is null ? null : Tiers.FirstOrDefault(t => string.Equals(t.Tier.Name, tier.Name, StringComparison.Ordinal));
    }



    /// <summary>
    /// Every way the table breaks the rules: a tier missing, a limit without a value in a tier, an unknown
    /// feature or limit name, a tier that grants less than a lower tier. Empty when the table is sound.
    /// </summary>
    public IReadOnlyList<string> Verify()
    {
        var problems = new List<string>();
        foreach (var tier in LicenseTiers.All)
        {
            if (Find(tier) is null)
            {
                problems.Add($"tier '{tier.Name}' has no definition in {Version}");
            }
        }

        foreach (var definition in Tiers)
        {
            foreach (var limit in LicenseLimits.All)
            {
                if (!definition.Limits.ContainsKey(limit.Name))
                {
                    problems.Add($"tier '{definition.Tier.Name}' has no value for limit '{limit.Name}'");
                }
            }

            problems.AddRange(definition.Limits.Keys.Where(k => LicenseLimits.Find(k) is null).Select(k => $"tier '{definition.Tier.Name}' values unknown limit '{k}'"));
            problems.AddRange(definition.Features.Where(f => LicenseFeatures.Find(f) is null).Select(f => $"tier '{definition.Tier.Name}' grants unknown feature '{f}'"));
        }

        foreach (var (lower, higher) in Tiers.Zip(Tiers.Skip(1)))
        {
            problems.AddRange(lower.Features.Except(higher.Features, StringComparer.Ordinal).Select(f => $"tier '{higher.Tier.Name}' lacks '{f}' which the lower tier '{lower.Tier.Name}' grants"));
            foreach (var limit in LicenseLimits.All.Where(l => lower.Limit(l).IsReducedBy(higher.Limit(l))))
            {
                problems.Add($"tier '{higher.Tier.Name}' values '{limit.Name}' below the lower tier '{lower.Tier.Name}' ({higher.Limit(limit)} < {lower.Limit(limit)})");
            }
        }

        return problems;
    }



    /// <summary>
    /// Every regression from <paramref name="previous"/> to <paramref name="current"/>: a tier removed, a
    /// feature no longer granted by a tier that granted it, a limit lowered. Empty when the upgrade only adds.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IReadOnlyList<string> Regressions(TierTable previous, TierTable current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var regressions = new List<string>();
        foreach (var before in previous.Tiers)
        {
            var after = current.Find(before.Tier);
            if (after is null)
            {
                regressions.Add($"tier '{before.Tier.Name}' was removed in {current.Version}");
                continue;
            }

            regressions.AddRange(before.Features.Except(after.Features, StringComparer.Ordinal).Select(f => $"tier '{before.Tier.Name}' lost feature '{f}' in {current.Version}"));
            foreach (var (name, value) in before.Limits)
            {
                var now = after.Limits.TryGetValue(name, out var v) ? v : LimitValue.Unlimited;
                if (value.IsReducedBy(now))
                {
                    regressions.Add($"tier '{before.Tier.Name}' limit '{name}' went from {value} to {now} in {current.Version}");
                }
            }
        }

        return regressions;
    }



    /// <summary>
    /// What <paramref name="current"/> adds over <paramref name="previous"/>, one line each (the release's
    /// "what changed" for the comparison page and the upgrade preflight).
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IReadOnlyList<string> Additions(TierTable previous, TierTable current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var additions = new List<string>();
        foreach (var after in current.Tiers)
        {
            var before = previous.Find(after.Tier);
            if (before is null)
            {
                additions.Add($"tier '{after.Tier.Name}' added");
                continue;
            }

            additions.AddRange(after.Features.Except(before.Features, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(f => $"tier '{after.Tier.Name}' gains '{f}'"));
            foreach (var (name, value) in after.Limits.OrderBy(l => l.Key, StringComparer.Ordinal))
            {
                var was = before.Limits.TryGetValue(name, out var v) ? v : LimitValue.Unlimited;
                if (was.IsReducedBy(value) is false && !value.Equals(was) && (was.IsUnlimited is false))
                {
                    additions.Add($"tier '{after.Tier.Name}' limit '{name}' raised from {was} to {value}");
                }
            }
        }

        return additions;
    }



    /// <summary>
    /// The table as lines of <c>tier|feature-or-limit|value</c>, sorted: the stable form written to the
    /// release's <c>tier-table</c> document and compared across releases.
    /// </summary>
    public IReadOnlyList<string> ToLines()
    {
        var lines = new List<string>();
        foreach (var definition in Tiers)
        {
            lines.AddRange(definition.Features.Order(StringComparer.Ordinal).Select(f => string.Create(CultureInfo.InvariantCulture, $"{definition.Tier.Name}|feature|{f}|granted")));
            lines.AddRange(definition.Limits.OrderBy(l => l.Key, StringComparer.Ordinal).Select(l => string.Create(CultureInfo.InvariantCulture, $"{definition.Tier.Name}|limit|{l.Key}|{l.Value}")));
        }

        return lines;
    }



    /// <summary>
    /// A table from the lines <see cref="ToLines"/> wrote (a previous release's document).
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="FormatException">A line is not <c>tier|feature|name|granted</c> or <c>tier|limit|name|value</c>.</exception>
    public static TierTable FromLines(string version, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var features = new Dictionary<string, List<LicenseFeature>>(StringComparer.Ordinal);
        var limits = new Dictionary<string, Dictionary<string, LimitValue>>(StringComparer.Ordinal);
        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            var parts = line.Split('|');
            if (parts.Length != 4)
            {
                throw new FormatException($"'{line}' is not a tier-table line.");
            }

            var tier = parts[0];
            features.TryAdd(tier, []);
            limits.TryAdd(tier, new Dictionary<string, LimitValue>(StringComparer.Ordinal));
            switch (parts[1])
            {
                case "feature":
                    features[tier].Add(LicenseFeatures.Find(parts[2]) ?? new LicenseFeature(parts[2], "(feature of another release)"));
                    break;
                case "limit":
                    limits[tier][parts[2]] = string.Equals(parts[3], "unlimited", StringComparison.Ordinal) ? LimitValue.Unlimited : LimitValue.Of(int.Parse(parts[3], CultureInfo.InvariantCulture));
                    break;
                default:
                    throw new FormatException($"'{line}' is not a tier-table line.");
            }
        }

        return new TierTable(version, features.Keys.Select(name => new TierDefinition(LicenseTiers.Find(name) ?? new LicenseTier(name, int.MaxValue), features[name], limits[name])).OrderBy(t => t.Tier.Rank).ToList());
    }
}
