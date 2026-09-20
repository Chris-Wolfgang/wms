// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text;
using Wolfgang.Wms.Core.Http.Idempotency;

namespace Wolfgang.Wms.UnitTests.Http;

public sealed class IdempotencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);



    [Theory]
    [InlineData("abc-123", true)]
    [InlineData("!", true)]
    [InlineData("~", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("has space", false)]
    [InlineData("tab\there", false)]
    [InlineData("ünïcode", false)]
    public void IdempotencyKey_accepts_visible_ascii_only(string? header, bool expected)
    {
        var ok = IdempotencyKey.TryParse(header, out var key);

        Assert.Equal(expected, ok);
        Assert.Equal(expected ? header : string.Empty, key.ToString());
    }



    [Fact]
    public void IdempotencyKey_is_at_most_128_characters()
    {
        Assert.True(IdempotencyKey.TryParse(new string('k', 128), out _));
        Assert.False(IdempotencyKey.TryParse(new string('k', 129), out _));
        Assert.Equal("Idempotency-Key", IdempotencyKey.HeaderName);
    }



    [Fact]
    public void Fingerprint_is_lower_case_hex_sha256_of_the_body()
    {
        Assert.Equal
        (
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Idempotency.Fingerprint(ReadOnlySpan<byte>.Empty)
        );
        Assert.Equal
        (
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            Idempotency.Fingerprint(Encoding.UTF8.GetBytes("abc"))
        );
    }



    [Fact]
    public void Decide_proceeds_when_nothing_is_stored_or_the_record_expired()
    {
        var record = CreateRecord("f1", Now - Idempotency.Retention);

        Assert.Equal(IdempotencyDecision.Proceed, Idempotency.Decide(null, "f1", Now));
        Assert.Equal(IdempotencyDecision.Proceed, Idempotency.Decide(record, "f1", Now));
        Assert.True(record.IsExpired(Now));
    }



    [Fact]
    public void Decide_replays_a_matching_body_and_conflicts_on_a_different_one()
    {
        var record = CreateRecord("f1", Now - TimeSpan.FromHours(23));

        Assert.Equal(IdempotencyDecision.Replay, Idempotency.Decide(record, "f1", Now));
        Assert.Equal(IdempotencyDecision.Conflict, Idempotency.Decide(record, "f2", Now));
        Assert.False(record.IsExpired(Now));
    }



    [Fact]
    public void Decide_and_the_record_require_a_caller_and_a_fingerprint()
    {
        Assert.Throws<ArgumentException>(() => Idempotency.Decide(null, " ", Now));
        Assert.Throws<ArgumentException>(() => CreateRecord(" ", Now));
        Assert.Throws<ArgumentException>(() => new IdempotencyRecord(" ", Key("k"), "f", 200, null, null, Now));
    }



    [Fact]
    public void Record_keeps_the_stored_response()
    {
        var record = new IdempotencyRecord("device-7", Key("k1"), "f1", 201, "application/json", [1, 2, 3], Now);

        Assert.Equal("device-7", record.Caller);
        Assert.Equal("k1", record.Key.Value);
        Assert.Equal(201, record.StatusCode);
        Assert.Equal("application/json", record.ContentType);
        Assert.Equal([1, 2, 3], record.Body);
        Assert.Equal(TimeSpan.FromHours(24), Idempotency.Retention);
    }



    private static IdempotencyRecord CreateRecord(string fingerprint, DateTimeOffset storedAt)
    {
        return new IdempotencyRecord("user-1", Key("k1"), fingerprint, 200, null, null, storedAt);
    }



    private static IdempotencyKey Key(string text)
    {
        Assert.True(IdempotencyKey.TryParse(text, out var key));
        return key;
    }
}
