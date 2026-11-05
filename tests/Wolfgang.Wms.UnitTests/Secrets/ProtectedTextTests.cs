// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Secrets;

namespace Wolfgang.Wms.UnitTests.Secrets;

/// <summary>
/// E8.2: the <c>enc:v1:</c> form, plain values passing through, and the message when a protected value
/// meets no protector.
/// </summary>
public sealed class ProtectedTextTests
{
    [Fact]
    public void Wrap_and_unwrap_round_the_prefix()
    {
        Assert.Equal("enc:v1:abc", ProtectedText.Wrap("abc"));
        Assert.Equal("abc", ProtectedText.Unwrap("enc:v1:abc"));
        Assert.True(ProtectedText.IsProtected("enc:v1:abc"));
        Assert.False(ProtectedText.IsProtected("Server=db"));
        Assert.False(ProtectedText.IsProtected(null));
        Assert.Throws<ArgumentException>(() => ProtectedText.Wrap(""));
        Assert.Throws<ArgumentException>(() => ProtectedText.Unwrap("Server=db"));
        Assert.Throws<ArgumentException>(() => ProtectedText.Unwrap("enc:v1:"));
    }



    [Fact]
    public void Reveal_passes_plain_text_through_and_needs_a_protector_for_the_rest()
    {
        var protector = new FakeProtector();

        Assert.Equal("Server=db", ProtectedText.Reveal("Server=db", protector: null));
        Assert.Null(ProtectedText.Reveal(null, protector));
        Assert.Equal("plain", ProtectedText.Reveal("enc:v1:plain", protector));
        Assert.Equal("enc:v1:plain", protector.Protect("plain"));
        var failure = Assert.Throws<InvalidOperationException>(() => ProtectedText.Reveal("enc:v1:plain", protector: null));
        Assert.Contains("Wms:DataProtection:KeyRingPath", failure.Message, StringComparison.Ordinal);
    }



    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("abc", "abc…")]
    [InlineData("enc:v1:CfDJ8ABCDEFGH", "enc:v1:CfDJ8…")]
    public void Mask_never_shows_a_whole_secret(string? text, string expected)
    {
        Assert.Equal(expected, ProtectedText.Mask(text));
    }



    private sealed class FakeProtector : ISecretProtector
    {
        public string Protect(string plainText)
        {
            return ProtectedText.Wrap(plainText);
        }

        public string Unprotect(string protectedText)
        {
            return ProtectedText.Unwrap(protectedText);
        }
    }
}
