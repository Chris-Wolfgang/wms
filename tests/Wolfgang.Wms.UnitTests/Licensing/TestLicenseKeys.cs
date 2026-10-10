// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Security.Cryptography;
using Wolfgang.Wms.Core.Licensing;
using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.UnitTests.Licensing;

/// <summary>
/// A test key pair and the keys it signs: what the vendor's tool does, with a pair that lives only in the
/// test process.
/// </summary>
internal sealed class TestLicenseKeys : IDisposable
{
    private readonly ECDsa _pair = ECDsa.Create(ECCurve.NamedCurves.nistP256);



    /// <summary>The public half, as the verifier takes it.</summary>
    public string PublicKey => Convert.ToBase64String(_pair.ExportSubjectPublicKeyInfo());



    /// <summary>A verifier for this pair.</summary>
    public LicenseVerifier Verifier()
    {
        return new LicenseVerifier(PublicKey);
    }



    /// <summary>Signs a key.</summary>
    public string Sign(LicenseKey key)
    {
        return LicenseKeySigner.Sign(key, _pair);
    }



    /// <summary>A pro base key for Acme covering 2026-09-20 to 2027-09-19.</summary>
    public static LicenseKey ProBase(string id = "b1", string organization = "Acme", IReadOnlyList<CoveragePeriod>? coverage = null, IReadOnlyList<string>? supersedes = null)
    {
        return new LicenseKey(1, id, LicenseKeyKind.Base, "pro", organization, coverage ?? [new CoveragePeriod(new DateOnly(2026, 9, 20), new DateOnly(2027, 9, 19))], ["reports.custom_views"], new Dictionary<string, LimitValue>(StringComparer.Ordinal) { ["sites"] = LimitValue.Of(3) }, Devices: 0, supersedes ?? [], new DateOnly(2026, 9, 1), AllowancePercent: 20, AllowanceMinimumUnits: 3, GraceDays: 45);
    }



    /// <summary>An add-on of ten devices for Acme.</summary>
    public static LicenseKey DevicesAddOn(string id = "a1", string organization = "Acme", int devices = 10)
    {
        return new LicenseKey(1, id, LicenseKeyKind.AddOn, Tier: null, organization, [], [], new Dictionary<string, LimitValue>(StringComparer.Ordinal) { ["max_totes_per_picker"] = LimitValue.Unlimited }, devices, [], new DateOnly(2026, 9, 2));
    }



    public void Dispose()
    {
        _pair.Dispose();
    }
}
