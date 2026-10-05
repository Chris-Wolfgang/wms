// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Web.Shared;

namespace Wolfgang.Wms.UnitTests.Web;

public sealed class ScanDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_when_no_screen_listens_reports_the_scan_unused()
    {
        var scans = new ScanDispatcher();

        var used = await scans.DispatchAsync("TOTE-0017");

        Assert.False(used);
        Assert.False(scans.HasListener);
    }



    [Fact]
    public async Task DispatchAsync_sends_the_scan_to_the_listening_screen()
    {
        var scans = new ScanDispatcher();
        var received = new List<string>();
        using var registration = scans.Listen(scan => Record(received, scan));

        var used = await scans.DispatchAsync("TOTE-0017");

        Assert.True(used);
        Assert.True(scans.HasListener);
        Assert.Equal(["TOTE-0017"], received);
    }



    [Fact]
    public async Task DispatchAsync_sends_to_the_most_recent_listener_until_it_stops()
    {
        var scans = new ScanDispatcher();
        var screen = new List<string>();
        var dialog = new List<string>();
        using var screenRegistration = scans.Listen(scan => Record(screen, scan));

        using (scans.Listen(scan => Record(dialog, scan)))
        {
            await scans.DispatchAsync("first");
        }

        await scans.DispatchAsync("second");

        Assert.Equal(["first"], dialog);
        Assert.Equal(["second"], screen);
    }



    [Fact]
    public async Task A_disposed_registration_no_longer_receives_scans()
    {
        var scans = new ScanDispatcher();
        var received = new List<string>();
        var registration = scans.Listen(scan => Record(received, scan));

        registration.Dispose();
        var used = await scans.DispatchAsync("TOTE-0017");

        Assert.False(used);
        Assert.Empty(received);
    }



    [Fact]
    public async Task Disposing_a_registration_removes_that_registration_when_a_callback_is_registered_twice()
    {
        var scans = new ScanDispatcher();
        var screen = new List<string>();
        var dialog = new List<string>();
        Func<string, Task> toScreen = scan => Record(screen, scan);
        using var first = scans.Listen(toScreen);
        using var dialogRegistration = scans.Listen(scan => Record(dialog, scan));
        var second = scans.Listen(toScreen);

        second.Dispose();
        await scans.DispatchAsync("TOTE-0017");

        Assert.Equal(["TOTE-0017"], dialog);
        Assert.Empty(screen);
    }



    [Fact]
    public async Task Listen_and_DispatchAsync_reject_null()
    {
        var scans = new ScanDispatcher();

        Assert.Equal("listener", Assert.Throws<ArgumentNullException>(() => scans.Listen(null!)).ParamName);
        Assert.Equal("scan", (await Assert.ThrowsAsync<ArgumentNullException>(() => scans.DispatchAsync(null!))).ParamName);
    }



    private static Task Record(List<string> received, string scan)
    {
        received.Add(scan);
        return Task.CompletedTask;
    }
}
