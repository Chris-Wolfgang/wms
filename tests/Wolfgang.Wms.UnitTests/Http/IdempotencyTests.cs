// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text;
using Wolfgang.Wms.Core.Http.Idempotency;

namespace Wolfgang.Wms.UnitTests.Http;

public sealed class IdempotencyTests
{
    private const string KeyText = "3f2a9c1e-7b4d-4e8a-9c0f-1d2e3f4a5b6c";

    private static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);



    [Theory]
    [InlineData("3f2a9c1e-7b4d-4e8a-9c0f-1d2e3f4a5b6c", true)]
    [InlineData("01J9ZK6Q4X8R2M5T7V3W9Y1B0C", true)]
    [InlineData("!!!!!!!!!!!!!!!!", true)]
    [InlineData("~~~~~~~~~~~~~~~~", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("abc-123", false)]
    [InlineData("has space in key", false)]
    [InlineData("tab\there-and-more", false)]
    [InlineData("ünïcode-key-12345", false)]
    public void IdempotencyKey_accepts_visible_ascii_only(string? header, bool expected)
    {
        var ok = IdempotencyKey.TryParse(header, out var key);

        Assert.Equal(expected, ok);
        Assert.Equal(expected ? header : string.Empty, key.ToString());
    }



    [Fact]
    public void IdempotencyKey_is_16_to_128_characters()
    {
        Assert.False(IdempotencyKey.TryParse(new string('k', IdempotencyKey.MinLength - 1), out _));
        Assert.True(IdempotencyKey.TryParse(new string('k', IdempotencyKey.MinLength), out _));
        Assert.True(IdempotencyKey.TryParse(new string('k', IdempotencyKey.MaxLength), out _));
        Assert.False(IdempotencyKey.TryParse(new string('k', IdempotencyKey.MaxLength + 1), out _));
        Assert.Equal(16, IdempotencyKey.MinLength);
        Assert.Equal(128, IdempotencyKey.MaxLength);
        Assert.Equal("Idempotency-Key", IdempotencyKey.HeaderName);
    }



    [Fact]
    public void Fingerprint_is_lower_case_hex_sha256_of_the_body()
    {
        Assert.Equal
        (
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            IdempotencyRules.Fingerprint(ReadOnlySpan<byte>.Empty)
        );
        Assert.Equal
        (
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            IdempotencyRules.Fingerprint(Encoding.UTF8.GetBytes("abc"))
        );
    }



    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("{\"sku\":\"A-1\",\"qty\":3}")]
    public void Fingerprint_is_64_lower_case_hex_characters(string body)
    {
        var fingerprint = IdempotencyRules.Fingerprint(Encoding.UTF8.GetBytes(body));

        Assert.Matches("^[0-9a-f]{64}$", fingerprint);
        Assert.Equal(fingerprint.ToLowerInvariant(), fingerprint);
    }



    [Fact]
    public void Decide_proceeds_when_nothing_is_stored_or_the_record_expired()
    {
        var record = CreateRecord("f1", Now - IdempotencyRules.Retention);

        Assert.Equal(IdempotencyDecision.Proceed, IdempotencyRules.Decide(null, "f1", Now));
        Assert.Equal(IdempotencyDecision.Proceed, IdempotencyRules.Decide(record, "f1", Now));
        Assert.True(record.IsExpired(Now));
    }



    [Fact]
    public void Decide_proceeds_for_an_expired_record_even_when_the_body_differs()
    {
        var record = CreateRecord("f1", Now - IdempotencyRules.Retention);

        Assert.Equal(IdempotencyDecision.Proceed, IdempotencyRules.Decide(record, "f2", Now));
    }



    [Fact]
    public void Decide_replays_a_matching_body_and_conflicts_on_a_different_one()
    {
        var record = CreateRecord("f1", Now - TimeSpan.FromHours(23));

        Assert.Equal(IdempotencyDecision.Replay, IdempotencyRules.Decide(record, "f1", Now));
        Assert.Equal(IdempotencyDecision.Conflict, IdempotencyRules.Decide(record, "f2", Now));
        Assert.False(record.IsExpired(Now));
    }



    [Fact]
    public void Decide_and_the_record_require_a_caller_and_a_fingerprint()
    {
        Assert.Equal("requestFingerprint", Assert.Throws<ArgumentException>(() => IdempotencyRules.Decide(null, " ", Now)).ParamName);
        Assert.Equal("RequestFingerprint", Assert.Throws<ArgumentException>(() => CreateRecord(" ", Now)).ParamName);
        Assert.Equal("Caller", Assert.Throws<ArgumentException>(() => new IdempotencyRecord(" ", Key(KeyText), "f", 200, null, null, Now)).ParamName);
    }



    [Fact]
    public void Record_keeps_the_stored_response()
    {
        var record = new IdempotencyRecord("device-7", Key(KeyText), "f1", 201, "application/json", [1, 2, 3], Now);

        Assert.Equal("device-7", record.Caller);
        Assert.Equal(KeyText, record.Key.Value);
        Assert.Equal(201, record.StatusCode);
        Assert.Equal("application/json", record.ContentType);
        Assert.Equal([1, 2, 3], record.Body);
        Assert.Equal(TimeSpan.FromHours(24), IdempotencyRules.Retention);
    }



    private static IdempotencyRecord CreateRecord(string fingerprint, DateTimeOffset storedAt)
    {
        return new IdempotencyRecord("user-1", Key(KeyText), fingerprint, 200, null, null, storedAt);
    }



    private static IdempotencyKey Key(string text)
    {
        Assert.True(IdempotencyKey.TryParse(text, out var key));
        return key;
    }
}
