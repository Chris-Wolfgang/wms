// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Wolfgang.Wms.Core.Licensing;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Licensing;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.UnitTests.Licensing;

/// <summary>
/// E79.3, E79.11: the state starts as the free tier, reads the stored documents line by line (an
/// unreadable line stays listed as invalid), and the sync refreshes it once the host has started and on
/// every tick, surviving a failing read. The settings and the totes cap are what the stories say.
/// </summary>
public sealed class LicenseStateTests
{
    [Fact]
    public async Task Stored_documents_become_the_license_and_bad_lines_are_flagged()
    {
        using var keys = new TestLicenseKeys();
        var settings = new FakeLicenseSettings();
        using var provider = Host(settings, keys.Verifier());
        var state = provider.GetRequiredService<LicenseState>();
        settings.Values[LicenseSettings.Keys.Name] = $"{keys.Sign(TestLicenseKeys.ProBase())}\r\n\n  {keys.Sign(TestLicenseKeys.DevicesAddOn())}  \nnot a key\n";

        var before = state.Current;
        var after = await state.RefreshAsync(CancellationToken.None);
        var read = state.Read("garbage");

        Assert.Equal("free", before.Tier.Name);
        Assert.Same(after, state.Current);
        Assert.Equal(("pro", "Acme", 10, 3), (after.Tier.Name, after.Organization, after.PurchasedDevices, after.Keys.Count));
        Assert.Equal((KeyStatus.Invalid, "line 3", "the text is not a signed key document"), (after.Keys[2].Status, after.Keys[2].KeyId, after.Keys[2].Reason));
        Assert.True(state.HasFeature(LicenseFeatures.ReportsCustomViews));
        Assert.Equal(LimitValue.Of(15), state.Limit(LicenseLimits.Devices));
        Assert.Single(read);
        Assert.Throws<ArgumentNullException>(() => state.Read(null!));
        Assert.Throws<ArgumentNullException>(() => LicenseState.Lines(null!));
        Assert.Throws<ArgumentNullException>(() => new LicenseState(null!, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<LicenseState>.Instance));
        Assert.Throws<ArgumentNullException>(() => new LicenseState(keys.Verifier(), null!, NullLogger<LicenseState>.Instance));
        Assert.Throws<ArgumentNullException>(() => new LicenseState(keys.Verifier(), provider.GetRequiredService<IServiceScopeFactory>(), null!));
    }



    [Fact]
    public async Task The_sync_refreshes_after_start_and_on_every_tick_and_survives_a_failing_read()
    {
        using var keys = new TestLicenseKeys();
        var settings = new FakeLicenseSettings { Failure = new InvalidOperationException("no store yet") };
        using var provider = Host(settings, keys.Verifier());
        var state = provider.GetRequiredService<LicenseState>();
        var time = new TestClock(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        using var lifetime = new StartedLifetime();
        using var sync = new LicenseSync(state, time, lifetime, NullLogger<LicenseSync>.Instance);

        await sync.StartAsync(CancellationToken.None);
        var beforeStart = state.Current.Tier.Name;
        lifetime.Start();
        await Task.Delay(50);
        settings.Failure = null;
        settings.Values[LicenseSettings.Keys.Name] = keys.Sign(TestLicenseKeys.ProBase());
        time.Advance(LicenseSync.Interval);
        await WaitForAsync(() => string.Equals(state.Current.Tier.Name, "pro", StringComparison.Ordinal));
        await sync.StopAsync(CancellationToken.None);

        Assert.Equal("free", beforeStart);
        Assert.Equal("pro", state.Current.Tier.Name);
        Assert.Equal(TimeSpan.FromSeconds(5), LicenseSync.Interval);
        Assert.Throws<ArgumentNullException>(() => new LicenseSync(null!, time, lifetime, NullLogger<LicenseSync>.Instance));
        Assert.Throws<ArgumentNullException>(() => new LicenseSync(state, null!, lifetime, NullLogger<LicenseSync>.Instance));
        Assert.Throws<ArgumentNullException>(() => new LicenseSync(state, time, null!, NullLogger<LicenseSync>.Instance));
        Assert.Throws<ArgumentNullException>(() => new LicenseSync(state, time, lifetime, null!));
    }



    [Fact]
    public async Task A_sync_started_after_the_host_refreshes_at_once()
    {
        using var keys = new TestLicenseKeys();
        var settings = new FakeLicenseSettings();
        settings.Values[LicenseSettings.Keys.Name] = keys.Sign(TestLicenseKeys.ProBase());
        using var provider = Host(settings, keys.Verifier());
        var state = provider.GetRequiredService<LicenseState>();
        using var lifetime = new StartedLifetime();
        lifetime.Start();
        using var sync = new LicenseSync(state, TimeProvider.System, lifetime, NullLogger<LicenseSync>.Instance);

        await sync.StartAsync(CancellationToken.None);
        await WaitForAsync(() => string.Equals(state.Current.Tier.Name, "pro", StringComparison.Ordinal));
        await sync.StopAsync(CancellationToken.None);

        Assert.Equal("pro", state.Current.Tier.Name);
    }



    [Fact]
    public void The_settings_and_the_totes_cap_follow_the_stories()
    {
        var free = LicenseComposer.Compose(TierTables.Current, [], new DateOnly(2026, 9, 20));
        var unlimited = new Wolfgang.Wms.Domain.Licensing.EffectiveLicense(LicenseTiers.Enterprise, "o", CoverageStatus.Covered, null, new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, LimitValue>(StringComparer.Ordinal), 0, 0, 10, 2, 30, []);

        Assert.Equal(SettingKind.Secret, LicenseSettings.Keys.Kind);
        Assert.Equal(SettingScopes.Organization, LicenseSettings.Keys.Scopes);
        Assert.Equal(80, LicenseSettings.UsageWarningPercent.DefaultValue);
        Assert.NotNull(LicenseSettings.UsageWarningPercent.Validator!(0));
        Assert.Null(LicenseSettings.UsageWarningPercent.Validator!(50));
        Assert.Equal(("picking.max_totes_per_picker", SettingScopes.OrganizationToZone, 1), (LicenseSettings.MaxTotesPerPicker.Name, LicenseSettings.MaxTotesPerPicker.Scopes, LicenseSettings.MaxTotesPerPicker.DefaultValue));
        Assert.NotNull(LicenseSettings.MaxTotesPerPicker.Validator!(0));
        Assert.Null(LicenseSettings.MaxTotesPerPicker.Validator!(3));
        Assert.Equal("license.overage_since.devices", LicenseSettings.OverageSince(LicenseLimits.Devices).Name);
        Assert.Equal(3 + LicenseLimits.All.Count, LicenseSettings.All.Count);
        Assert.Equal(1, LicenseSettings.EffectiveMaxTotesPerPicker(4, free));   // the free tier caps at 1
        Assert.Equal(4, LicenseSettings.EffectiveMaxTotesPerPicker(4, unlimited));
        Assert.Throws<ArgumentNullException>(() => LicenseSettings.OverageSince(null!));
        Assert.Throws<ArgumentNullException>(() => LicenseSettings.EffectiveMaxTotesPerPicker(1, null!));
        Assert.Equal(LicenseSettings.All.Count, LicenseModule.Descriptor.Settings.Count);
        Assert.Equal(LicenseFeatures.All.Count, LicenseModule.Descriptor.LicenseFeatures.Count);
        Assert.Equal(["license.read", "license.manage"], LicenseModule.Descriptor.Permissions.Select(p => p.Name));
        Assert.Throws<ArgumentNullException>(() => LicenseModule.AddWmsLicenseModule(null!));
    }



    [Fact]
    public async Task The_test_doubles_behave()
    {
        var settings = new FakeLicenseSettings();
        var clock = new TestClock(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        var fired = 0;
        var lifetime = new StartedLifetime();
        var scope = SettingScopeRef.Organization;

        await using (var timer = clock.CreateTimer(_ => fired++, state: null, TimeSpan.FromSeconds(1), TimeSpan.Zero))
        {
            clock.Advance(TimeSpan.FromSeconds(2));
            clock.Advance(TimeSpan.FromSeconds(2));   // a one-shot timer fires once
            timer.Change(Timeout.InfiniteTimeSpan, TimeSpan.Zero);
            clock.Advance(TimeSpan.FromHours(1));
        }

        lifetime.StopApplication();
        lifetime.Dispose();

        Assert.Equal(1, fired);
        Assert.False(lifetime.ApplicationStopping.IsCancellationRequested);
        Assert.False(lifetime.ApplicationStopped.IsCancellationRequested);
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.ResetAsync(LicenseSettings.Keys, scope, "u", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.SetModeAsync(LicenseSettings.Keys, scope, CascadeMode.Value, "u", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.PopulateAsync(scope, "u", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.ListAsync(scope, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() => settings.SetTextAsync("license.keys", scope, "x", "u", CancellationToken.None));
    }



    internal static ServiceProvider Host(ISettings settings, LicenseVerifier verifier)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(verifier);
        services.AddSingleton<LicenseState>();
        services.AddSingleton<ILicense>(p => p.GetRequiredService<LicenseState>());
        services.AddScoped(_ => settings);
        return services.BuildServiceProvider();
    }



    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(25);
        }
    }



    private sealed class StartedLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _started = new();

        public CancellationToken ApplicationStarted => _started.Token;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void Start()
        {
            _started.Cancel();
        }

        public void StopApplication()
        {
        }

        public void Dispose()
        {
            _started.Dispose();
        }
    }
}
