// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using System.Security.Cryptography;
using Wolfgang.Wms.Core.Licensing;
using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.AotSmoke;

/// <summary>
/// License keys are signed and verified through <c>System.Text.Json</c> with the source-generated
/// <c>LicenseJsonContext</c> (snake_case, nulls omitted, DateOnly, enums, dictionaries of <see cref="LimitValue"/>)
/// and ECDSA P-256. Under NativeAOT the serializer must work from the generated metadata alone and the crypto from
/// the platform library, so a full sign → verify round trip, tampering and the embedded vendor key run here.
/// </summary>
internal static class LicensingSmoke
{
    public static void SignedKeyRoundTrips()
    {
        using var pair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var verifier = new LicenseVerifier(Convert.ToBase64String(pair.ExportSubjectPublicKeyInfo()));
        var key = SampleKey();

        var reading = verifier.Read(LicenseKeySigner.Sign(key, pair));

        var read = reading.Key ?? throw new SmokeFailureException($"a freshly signed key was refused: {reading.Reason}");
        Smoke.SequenceEqual([Describe(key)], [Describe(read)]);
    }



    public static void AlteredKeyIsRefused()
    {
        using var pair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var verifier = new LicenseVerifier(Convert.ToBase64String(pair.ExportSubjectPublicKeyInfo()));

        var foreign = verifier.Read(LicenseKeySigner.Sign(SampleKey(), other));
        var garbage = verifier.Read("{ not a key");

        Smoke.SequenceEqual([bool.TrueString, bool.TrueString], [(foreign.Key is null).ToString(), (garbage.Key is null).ToString()]);
        Smoke.SequenceEqual([bool.TrueString], [(foreign.Reason?.Contains("signature", StringComparison.Ordinal) ?? false).ToString()]);
    }



    public static void VendorKeyLoads()
    {
        using var vendor = LicenseVerifier.ForVendor();

        var reading = vendor.Read("{\"alg\":\"ES256\"}");

        Smoke.SequenceEqual([bool.TrueString], [(reading.Key is null).ToString()]);
    }



    private static LicenseKey SampleKey()
    {
        return new LicenseKey
        (
            LicenseKey.CurrentSchemaVersion,
            "smoke-1",
            LicenseKeyKind.Base,
            "pro",
            "Acme",
            [new CoveragePeriod(new DateOnly(2026, 9, 20), new DateOnly(2027, 9, 19))],
            ["reports.custom_views"],
            new Dictionary<string, LimitValue>(StringComparer.Ordinal) { ["sites"] = LimitValue.Of(3), ["devices_extra"] = LimitValue.Unlimited },
            Devices: 5,
            ["old-1"],
            new DateOnly(2026, 9, 1),
            AllowancePercent: 20,
            AllowanceMinimumUnits: 3,
            GraceDays: 45
        );
    }



    private static string Describe(LicenseKey key)
    {
        var limits = string.Join(",", key.Limits.OrderBy(l => l.Key, StringComparer.Ordinal).Select(l => $"{l.Key}={(l.Value.IsUnlimited ? "unlimited" : l.Value.Value!.Value.ToString(CultureInfo.InvariantCulture))}"));
        var coverage = string.Join(",", key.Coverage.Select(c => $"{c.From:yyyy-MM-dd}..{c.To:yyyy-MM-dd}"));
        return string.Join
        (
            "|",
            key.SchemaVersion.ToString(CultureInfo.InvariantCulture),
            key.KeyId,
            key.Kind.ToString(),
            key.Tier ?? "-",
            key.Organization,
            coverage,
            string.Join(",", key.Features),
            limits,
            key.Devices.ToString(CultureInfo.InvariantCulture),
            string.Join(",", key.Supersedes),
            key.IssuedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            key.AllowancePercent?.ToString(CultureInfo.InvariantCulture) ?? "-",
            key.AllowanceMinimumUnits?.ToString(CultureInfo.InvariantCulture) ?? "-",
            key.GraceDays?.ToString(CultureInfo.InvariantCulture) ?? "-"
        );
    }
}
