// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// Converts between the JSON payload and the domain key (E79.1, E79.3), validating on the way in: a
/// document from a newer schema, an unknown kind or tier, an inverted coverage period or a negative count
/// is refused with a reason an administrator can act on.
/// </summary>
internal static class LicenseKeyJson
{
    /// <summary>
    /// The domain key for a payload.
    /// </summary>
    /// <exception cref="FormatException">The payload is not a valid key of this release's schema.</exception>
    public static LicenseKey FromPayload(LicenseKeyPayload payload)
    {
        if (payload.SchemaVersion < 1)
        {
            throw new FormatException("schema_version is missing");
        }

        if (payload.SchemaVersion > LicenseKey.CurrentSchemaVersion)
        {
            throw new FormatException(string.Create(CultureInfo.InvariantCulture, $"the key uses schema {payload.SchemaVersion}; this release reads schema {LicenseKey.CurrentSchemaVersion} — upgrade first"));
        }

        var keyId = Required(payload.KeyId, "key_id");
        var kind = payload.Kind switch
        {
            "base" => LicenseKeyKind.Base,
            "add_on" => LicenseKeyKind.AddOn,
            _ => throw new FormatException("kind must be 'base' or 'add_on'"),
        };
        var organization = Required(payload.Organization, "organization");
        var coverage = (payload.Coverage ?? []).Select(Period).ToList();
        if (kind == LicenseKeyKind.Base)
        {
            RequireBase(payload, coverage);
        }

        if (payload.IssuedAt == default)
        {
            throw new FormatException("issued_at is missing");
        }

        var limits = (payload.Limits ?? new Dictionary<string, int?>(StringComparer.Ordinal)).ToDictionary(l => l.Key, l => Limit(l.Key, l.Value), StringComparer.Ordinal);
        return new LicenseKey
        (
            payload.SchemaVersion,
            keyId,
            kind,
            payload.Tier,
            organization,
            coverage,
            payload.Features ?? [],
            limits,
            NonNegative(payload.Devices, "devices"),
            payload.Supersedes ?? [],
            payload.IssuedAt,
            Percent(payload.AllowancePercent),
            payload.AllowanceMinimumUnits is { } units ? NonNegative(units, "allowance_minimum_units") : null,
            payload.GraceDays is { } days ? NonNegative(days, "grace_days") : null
        );
    }



    /// <summary>
    /// The payload for a domain key (what the vendor's tool signs).
    /// </summary>
    public static LicenseKeyPayload ToPayload(LicenseKey key)
    {
        return new LicenseKeyPayload
        {
            SchemaVersion = key.SchemaVersion,
            KeyId = key.KeyId,
            Kind = key.Kind == LicenseKeyKind.Base ? "base" : "add_on",
            Tier = key.Tier,
            Organization = key.Organization,
            Coverage = key.Coverage.Select(p => new CoveragePeriodPayload { From = p.From, To = p.To }).ToList(),
            Features = key.Features,
            Limits = key.Limits.ToDictionary(l => l.Key, l => l.Value.Value, StringComparer.Ordinal),
            Devices = key.Devices,
            Supersedes = key.Supersedes,
            IssuedAt = key.IssuedAt,
            AllowancePercent = key.AllowancePercent,
            AllowanceMinimumUnits = key.AllowanceMinimumUnits,
            GraceDays = key.GraceDays,
        };
    }



    private static void RequireBase(LicenseKeyPayload payload, List<CoveragePeriod> coverage)
    {
        if (LicenseTiers.Find(payload.Tier) is null)
        {
            throw new FormatException($"tier '{payload.Tier}' is not one this release knows (free, pro, enterprise) — upgrade first");
        }

        if (coverage.Count == 0)
        {
            throw new FormatException("a base key needs at least one coverage period");
        }
    }



    private static string Required(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\n', StringComparison.Ordinal))
        {
            throw new FormatException($"{name} is missing");
        }

        return value;
    }



    private static CoveragePeriod Period(CoveragePeriodPayload period)
    {
        if (period.From == default || period.To < period.From)
        {
            throw new FormatException("a coverage period needs 'from' on or before 'to'");
        }

        return new CoveragePeriod(period.From, period.To);
    }



    private static LimitValue Limit(string name, int? value)
    {
        return value is { } count ? LimitValue.Of(NonNegative(count, $"limits.{name}")) : LimitValue.Unlimited;
    }



    private static int NonNegative(int value, string name)
    {
        return value >= 0 ? value : throw new FormatException($"{name} must not be negative");
    }



    private static int? Percent(int? value)
    {
        return value is null or (>= 0 and <= 100) ? value : throw new FormatException("allowance_percent must be between 0 and 100");
    }
}
