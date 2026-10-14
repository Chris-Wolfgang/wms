// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Wolfgang.Wms.Core.Licensing;
using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.UnitTests.Licensing;

/// <summary>
/// E79.3: a signed key round-trips through the verifier with every field; a tampered payload, a foreign
/// signature, another algorithm, another schema or a malformed content is refused with the reason.
/// </summary>
public sealed class LicenseKeyTests
{
    [Fact]
    public void A_signed_key_round_trips()
    {
        using var keys = new TestLicenseKeys();
        using var verifier = keys.Verifier();
        var original = TestLicenseKeys.ProBase();
        var document = keys.Sign(original);

        var reading = verifier.Read(document);
        var addOn = verifier.Read(keys.Sign(TestLicenseKeys.DevicesAddOn()));

        Assert.Equal(original, reading.Key! with { Coverage = original.Coverage, Features = original.Features, Limits = original.Limits, Supersedes = original.Supersedes });
        Assert.Equal(original.Coverage, reading.Key.Coverage);
        Assert.Equal(original.Features, reading.Key.Features);
        Assert.Equal(original.Limits, reading.Key.Limits);
        Assert.Equal(document, reading.Document);
        Assert.Null(reading.Reason);
        Assert.DoesNotContain('\n', document);
        Assert.Contains("\"algorithm\":\"ES256\"", document, StringComparison.Ordinal);
        Assert.Equal((LicenseKeyKind.AddOn, 10, true), (addOn.Key!.Kind, addOn.Key.Devices, addOn.Key.Limits["max_totes_per_picker"].IsUnlimited));
        Assert.Contains("\"kind\":\"add_on\"", Encoding.UTF8.GetString(Base64Url.DecodeFromChars(JsonDocument.Parse(addOn.Document!).RootElement.GetProperty("payload").GetString()!)), StringComparison.Ordinal);
    }



    [Fact]
    public void Tampered_foreign_and_malformed_documents_are_refused()
    {
        using var keys = new TestLicenseKeys();
        using var other = new TestLicenseKeys();
        using var verifier = keys.Verifier();
        var document = keys.Sign(TestLicenseKeys.ProBase());
        using var parsed = JsonDocument.Parse(document);
        var signature = parsed.RootElement.GetProperty("signature").GetString()!;
        var tamperedPayload = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("{\"schema_version\":1,\"key_id\":\"evil\",\"kind\":\"base\",\"tier\":\"enterprise\",\"organization\":\"Acme\",\"coverage\":[{\"from\":\"2026-01-01\",\"to\":\"2099-01-01\"}],\"issued_at\":\"2026-01-01\"}"));

        Assert.Equal("the signature does not verify: the key was not issued by the vendor or has been altered", verifier.Read(other.Sign(TestLicenseKeys.ProBase())).Reason);
        Assert.Equal("the signature does not verify: the key was not issued by the vendor or has been altered", verifier.Read($"{{\"payload\":\"{tamperedPayload}\",\"signature\":\"{signature}\",\"algorithm\":\"ES256\"}}").Reason);
        Assert.Equal("the key is signed with 'RS256'; this release verifies ES256", verifier.Read(document.Replace("ES256", "RS256", StringComparison.Ordinal)).Reason);
        Assert.Equal("the text is not a signed key document", verifier.Read("not json").Reason);
        Assert.Equal("the text is not a signed key document", verifier.Read("{}").Reason);
        Assert.Equal("the text is not a signed key document", verifier.Read("null").Reason);
        Assert.Equal("the text is not a signed key document", verifier.Read("{\"payload\":\"!!\",\"signature\":\"AA\"}").Reason);
        Assert.Null(verifier.Read("{}").Document);
        Assert.Throws<ArgumentNullException>(() => verifier.Read(null!));
        Assert.Throws<ArgumentException>(() => new LicenseVerifier(" "));
        Assert.Throws<ArgumentNullException>(() => LicenseKeySigner.Sign(null!, System.Security.Cryptography.ECDsa.Create()));
        Assert.Throws<ArgumentNullException>(() => LicenseKeySigner.Sign(TestLicenseKeys.ProBase(), null!));
        using var vendor = LicenseVerifier.ForVendor();
        Assert.NotNull(vendor.Read(document).Reason);   // a test pair is not the vendor's
    }



    [Theory]
    [InlineData("{\"key_id\":\"k\"}", "schema_version is missing")]
    [InlineData("{\"schema_version\":2}", "the key uses schema 2; this release reads schema 1 — upgrade first")]
    [InlineData("{\"schema_version\":1}", "key_id is missing")]
    [InlineData("{\"schema_version\":1,\"key_id\":\"k\",\"kind\":\"gift\"}", "kind must be 'base' or 'add_on'")]
    [InlineData("{\"schema_version\":1,\"key_id\":\"k\",\"kind\":\"base\"}", "organization is missing")]
    [InlineData("{\"schema_version\":1,\"key_id\":\"k\",\"kind\":\"base\",\"organization\":\"o\",\"tier\":\"gold\",\"coverage\":[{\"from\":\"2026-01-01\",\"to\":\"2026-12-31\"}]}", "tier 'gold' is not one this release knows (free, pro, enterprise) — upgrade first")]
    [InlineData("{\"schema_version\":1,\"key_id\":\"k\",\"kind\":\"base\",\"organization\":\"o\",\"tier\":\"pro\"}", "a base key needs at least one coverage period")]
    [InlineData("{\"schema_version\":1,\"key_id\":\"k\",\"kind\":\"base\",\"organization\":\"o\",\"tier\":\"pro\",\"coverage\":[{\"from\":\"2026-12-31\",\"to\":\"2026-01-01\"}]}", "a coverage period needs 'from' on or before 'to'")]
    [InlineData("{\"schema_version\":1,\"key_id\":\"k\",\"kind\":\"add_on\",\"organization\":\"o\"}", "issued_at is missing")]
    [InlineData("{\"schema_version\":1,\"key_id\":\"k\",\"kind\":\"add_on\",\"organization\":\"o\",\"issued_at\":\"2026-01-01\",\"devices\":-1}", "devices must not be negative")]
    [InlineData("{\"schema_version\":1,\"key_id\":\"k\",\"kind\":\"add_on\",\"organization\":\"o\",\"issued_at\":\"2026-01-01\",\"limits\":{\"sites\":-1}}", "limits.sites must not be negative")]
    [InlineData("{\"schema_version\":1,\"key_id\":\"k\",\"kind\":\"add_on\",\"organization\":\"o\",\"issued_at\":\"2026-01-01\",\"allowance_percent\":101}", "allowance_percent must be between 0 and 100")]
    [InlineData("{\"schema_version\":1,\"key_id\":\"k\",\"kind\":\"add_on\",\"organization\":\"o\",\"issued_at\":\"2026-01-01\",\"allowance_minimum_units\":-1}", "allowance_minimum_units must not be negative")]
    [InlineData("{\"schema_version\":1,\"key_id\":\"k\",\"kind\":\"add_on\",\"organization\":\"o\",\"issued_at\":\"2026-01-01\",\"grace_days\":-1}", "grace_days must not be negative")]
    [InlineData("null", "the key content is empty")]
    [InlineData("nope", "the key content is not valid JSON")]
    public void Invalid_content_is_refused_with_the_reason(string payload, string reason)
    {
        using var keys = new TestLicenseKeys();
        using var verifier = keys.Verifier();

        Assert.Equal(reason, verifier.Read(SignRaw(keys, payload)).Reason);
    }



    [Fact]
    public void Minimal_valid_content_fills_the_defaults()
    {
        using var keys = new TestLicenseKeys();
        using var verifier = keys.Verifier();

        var key = verifier.Read(SignRaw(keys, "{\"schema_version\":1,\"key_id\":\"k\",\"kind\":\"add_on\",\"organization\":\"o\",\"issued_at\":\"2026-01-01\",\"limits\":{\"users\":null}}")).Key!;

        Assert.Equal((LicenseKeyKind.AddOn, 0, true, true), (key.Kind, key.Devices, key.IsPerpetual, key.Limits["users"].IsUnlimited));
        Assert.Empty(key.Features);
        Assert.Empty(key.Supersedes);
        Assert.Null(key.Tier);
        Assert.Null(key.AllowancePercent);
    }



    private static string SignRaw(TestLicenseKeys keys, string payloadJson)
    {
        // Sign a real key, then swap the payload for the raw content and re-sign through the same pair: the
        // envelope shape is the signer's, the content is the test's.
        using var pair = typeof(TestLicenseKeys).GetField("_pair", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(keys) as System.Security.Cryptography.ECDsa;
        var bytes = Encoding.UTF8.GetBytes(payloadJson);
        var signature = pair!.SignData(bytes, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{{\"payload\":\"{Base64Url.EncodeToString(bytes)}\",\"signature\":\"{Base64Url.EncodeToString(signature)}\",\"algorithm\":\"ES256\"}}";
    }
}
