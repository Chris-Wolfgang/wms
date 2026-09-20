// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Web.Shared;

namespace Wolfgang.Wms.UnitTests.Web;

public sealed class ScanBufferTests
{
    [Fact]
    public void Enter_completes_the_trimmed_scan_and_clears_the_buffer()
    {
        var buffer = new ScanBuffer();
        buffer.Update("  TOTE-0017 ");

        var completed = buffer.TryComplete("Enter", out var scanned);

        Assert.True(completed);
        Assert.Equal("TOTE-0017", scanned);
        Assert.Equal(string.Empty, buffer.Text);
    }



    [Theory]
    [InlineData("a")]
    [InlineData("Tab")]
    [InlineData(null)]
    public void Other_keys_leave_the_buffer_alone(string? key)
    {
        var buffer = new ScanBuffer();
        buffer.Update("TOTE");

        var completed = buffer.TryComplete(key, out var scanned);

        Assert.False(completed);
        Assert.Null(scanned);
        Assert.Equal("TOTE", buffer.Text);
    }



    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_blank_scan_is_ignored_but_still_clears_the_buffer(string? text)
    {
        var buffer = new ScanBuffer();
        buffer.Update(text);

        var completed = buffer.TryComplete(ScanBuffer.CompleteKey, out var scanned);

        Assert.False(completed);
        Assert.Null(scanned);
        Assert.Equal(string.Empty, buffer.Text);
    }
}
