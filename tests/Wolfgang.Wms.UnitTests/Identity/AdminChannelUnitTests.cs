// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Admin;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Identity.BreakGlass;
using Wolfgang.Wms.Core.Identity.BreakGlass.AdminChannel;
using Wolfgang.Wms.Core.Identity.Providers;

namespace Wolfgang.Wms.UnitTests.Identity;

/// <summary>
/// E9.3 without a pipe or a database: a sealed request round-trips and is refused when it was sealed with
/// another ring, is not a message, names an unknown command or is stale; the handler prefers the user the
/// transport authenticated, applies the default window, and turns refusals into answers; the registration
/// reads <c>Wms:Admin</c>; the tool's command line parses and refuses what it should; the tool explains a
/// missing ring and an unreachable host.
/// </summary>
public sealed class AdminChannelUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);



    [Fact]
    public void A_sealed_request_round_trips_and_a_foreign_seal_is_refused()
    {
        var ring = new EphemeralDataProtectionProvider().CreateProtector(AdminChannelProtocol.Purpose);
        var otherRing = new EphemeralDataProtectionProvider().CreateProtector(AdminChannelProtocol.Purpose);
        var request = new AdminChannelRequest(AdminChannelCommands.Unlock, 15, "HOST\\ops", Now);

        var line = AdminChannelProtocol.Seal(request, ring);
        var opened = AdminChannelProtocol.Open(line, ring, Now.AddSeconds(30));
        var foreign = Assert.Throws<InvalidOperationException>(() => AdminChannelProtocol.Open(line, otherRing, Now));

        Assert.DoesNotContain('\n', line);
        Assert.Equal(request, opened);
        Assert.Contains("not sealed with this host's key ring", foreign.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => AdminChannelProtocol.Seal(null!, ring));
        Assert.Throws<ArgumentNullException>(() => AdminChannelProtocol.Open(null!, ring, Now));
    }



    [Fact]
    public void A_request_that_is_not_a_message_unknown_or_stale_is_refused()
    {
        var ring = new EphemeralDataProtectionProvider().CreateProtector(AdminChannelProtocol.Purpose);

        var notJson = Assert.Throws<InvalidOperationException>(() => AdminChannelProtocol.Open(ring.Protect("hello"), ring, Now));
        var noUser = Assert.Throws<InvalidOperationException>(() => AdminChannelProtocol.Open(AdminChannelProtocol.Seal(new AdminChannelRequest(AdminChannelCommands.Status, null, " ", Now), ring), ring, Now));
        var unknown = Assert.Throws<InvalidOperationException>(() => AdminChannelProtocol.Open(AdminChannelProtocol.Seal(new AdminChannelRequest("reboot", null, "HOST\\ops", Now), ring), ring, Now));
        var stale = Assert.Throws<InvalidOperationException>(() => AdminChannelProtocol.Open(AdminChannelProtocol.Seal(new AdminChannelRequest(AdminChannelCommands.Lock, null, "HOST\\ops", Now), ring), ring, Now.AddMinutes(3)));
        var early = Assert.Throws<InvalidOperationException>(() => AdminChannelProtocol.Open(AdminChannelProtocol.Seal(new AdminChannelRequest(AdminChannelCommands.Lock, null, "HOST\\ops", Now), ring), ring, Now.AddMinutes(-3)));

        Assert.Contains("not a channel message", notJson.Message, StringComparison.Ordinal);
        Assert.Contains("not a channel message", noUser.Message, StringComparison.Ordinal);
        Assert.Contains("'reboot' is not a channel command", unknown.Message, StringComparison.Ordinal);
        Assert.Contains("outside the 2-minute window", stale.Message, StringComparison.Ordinal);
        Assert.Contains("outside the 2-minute window", early.Message, StringComparison.Ordinal);
        Assert.True(AdminChannelCommands.IsKnown(AdminChannelCommands.Status));
        Assert.False(AdminChannelCommands.IsKnown(null));
    }



    [Fact]
    public void A_response_round_trips_and_garbage_decodes_to_null()
    {
        var response = new AdminChannelResponse(Ok: true, "done", new LocalLoginStatus(true, false, Now, false));

        var line = AdminChannelProtocol.Encode(response);

        Assert.Equal(response, AdminChannelProtocol.Decode(line));
        Assert.Null(AdminChannelProtocol.Decode(null));
        Assert.Null(AdminChannelProtocol.Decode("  "));
        Assert.Null(AdminChannelProtocol.Decode("{not json"));
        Assert.Throws<ArgumentNullException>(() => AdminChannelProtocol.Encode(null!));
    }



    [Fact]
    public async Task The_handler_prefers_the_authenticated_user_applies_the_default_window_and_answers_refusals()
    {
        var gate = new FakeGate();
        await using var services = Host(gate, forceLocal: false);
        var handler = services.GetRequiredService<AdminChannelHandler>();
        var ring = services.GetRequiredService<IDataProtectionProvider>().CreateProtector(AdminChannelProtocol.Purpose);

        var unlocked = await handler.HandleAsync(AdminChannelProtocol.Seal(new AdminChannelRequest(AdminChannelCommands.Unlock, null, "claimed", Now), ring), "HOST\\svc", CancellationToken.None);
        var unlockedAgain = await handler.HandleAsync(AdminChannelProtocol.Seal(new AdminChannelRequest(AdminChannelCommands.Unlock, 10, "claimed", Now), ring), connectedOsUser: null, CancellationToken.None);
        var status = await handler.HandleAsync(AdminChannelProtocol.Seal(new AdminChannelRequest(AdminChannelCommands.Status, null, "claimed", Now), ring), "HOST\\svc", CancellationToken.None);
        var locked = await handler.HandleAsync(AdminChannelProtocol.Seal(new AdminChannelRequest(AdminChannelCommands.Lock, null, "claimed", Now), ring), "HOST\\svc", CancellationToken.None);
        var tooLong = await handler.HandleAsync(AdminChannelProtocol.Seal(new AdminChannelRequest(AdminChannelCommands.Unlock, 999_999, "claimed", Now), ring), "HOST\\svc", CancellationToken.None);
        var foreign = await handler.HandleAsync(new EphemeralDataProtectionProvider().CreateProtector(AdminChannelProtocol.Purpose).Protect("{}"), "HOST\\svc", CancellationToken.None);

        Assert.True(unlocked.Ok);
        Assert.Equal(("HOST\\svc", TimeSpan.FromMinutes(30)), gate.Unlocks[0]);
        Assert.Equal(("claimed", TimeSpan.FromMinutes(10)), gate.Unlocks[1]);
        Assert.Contains("unlocked until", unlocked.Message, StringComparison.Ordinal);
        Assert.True(unlocked.Status!.LocalLoginOpen);
        Assert.True(unlockedAgain.Ok);
        Assert.True(status.Ok);
        Assert.Contains("open until", status.Message, StringComparison.Ordinal);
        Assert.True(locked.Ok);
        Assert.Equal("HOST\\svc", gate.LockedBy);
        Assert.StartsWith("Window closed", locked.Message, StringComparison.Ordinal);   // SSO is unverified on this fake, so sign-in stays open
        Assert.False(tooLong.Ok);
        Assert.Contains("unlock window must be", tooLong.Message, StringComparison.Ordinal);
        Assert.Null(tooLong.Status);
        Assert.False(foreign.Ok);
        Assert.Contains("not sealed", foreign.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.HandleAsync(null!, null, CancellationToken.None));
    }



    [Fact]
    public async Task The_handler_reports_a_closed_and_a_forced_open_gate_and_the_placeholder_refusal()
    {
        var gate = new FakeGate { Info = LocalLoginGateInfo.Open with { SsoVerifiedAt = Now.AddDays(-1), SsoVerifiedProvider = "oidc" } };
        await using var closed = Host(gate, forceLocal: false);
        await using var forced = Host(gate, forceLocal: true);
        await using var placeholder = Host(new NoLocalLoginGate(), forceLocal: false);
        var ring = closed.GetRequiredService<IDataProtectionProvider>().CreateProtector(AdminChannelProtocol.Purpose);
        var forcedRing = forced.GetRequiredService<IDataProtectionProvider>().CreateProtector(AdminChannelProtocol.Purpose);
        var placeholderRing = placeholder.GetRequiredService<IDataProtectionProvider>().CreateProtector(AdminChannelProtocol.Purpose);
        var statusLine = new AdminChannelRequest(AdminChannelCommands.Status, null, "HOST\\ops", Now);

        var closedStatus = await closed.GetRequiredService<AdminChannelHandler>().HandleAsync(AdminChannelProtocol.Seal(statusLine, ring), null, CancellationToken.None);
        var closedLock = await closed.GetRequiredService<AdminChannelHandler>().HandleAsync(AdminChannelProtocol.Seal(new AdminChannelRequest(AdminChannelCommands.Lock, null, "HOST\\ops", Now), ring), null, CancellationToken.None);
        var forcedStatus = await forced.GetRequiredService<AdminChannelHandler>().HandleAsync(AdminChannelProtocol.Seal(statusLine, forcedRing), null, CancellationToken.None);
        var forcedLock = await forced.GetRequiredService<AdminChannelHandler>().HandleAsync(AdminChannelProtocol.Seal(new AdminChannelRequest(AdminChannelCommands.Lock, null, "HOST\\ops", Now), forcedRing), null, CancellationToken.None);
        var placeholderUnlock = await placeholder.GetRequiredService<AdminChannelHandler>().HandleAsync(AdminChannelProtocol.Seal(new AdminChannelRequest(AdminChannelCommands.Unlock, null, "HOST\\ops", Now), placeholderRing), null, CancellationToken.None);
        var placeholderStatus = await placeholder.GetRequiredService<AdminChannelHandler>().HandleAsync(AdminChannelProtocol.Seal(statusLine, placeholderRing), null, CancellationToken.None);

        Assert.Contains("closed (single sign-on verified", closedStatus.Message, StringComparison.Ordinal);
        Assert.Contains("local sign-in is closed", closedLock.Message, StringComparison.Ordinal);
        Assert.Equal("Local sign-in is open.", forcedStatus.Message);
        Assert.True(forcedStatus.Status!.ForcedLocal);
        Assert.Contains("stays open for another reason", forcedLock.Message, StringComparison.Ordinal);
        Assert.False(placeholderUnlock.Ok);
        Assert.Contains("database is not configured", placeholderUnlock.Message, StringComparison.Ordinal);
        Assert.True(placeholderStatus.Ok);
        Assert.Equal("Local sign-in is open.", placeholderStatus.Message);
    }



    [Fact]
    public void The_registration_reads_the_name_and_the_switch_from_configuration()
    {
        var configured = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [AdminChannelOptions.NameKey] = " lab-2 ", [AdminChannelOptions.EnabledKey] = "false" }).Build();
        var defaults = new ConfigurationBuilder().Build();

        var registrations = new ServiceCollection().AddWmsAdminChannel(configured);
        using var withValues = registrations.BuildServiceProvider();
        using var withDefaults = new ServiceCollection().AddWmsAdminChannel(defaults).BuildServiceProvider();
        var options = withValues.GetRequiredService<IOptions<AdminChannelOptions>>().Value;
        var defaultOptions = withDefaults.GetRequiredService<IOptions<AdminChannelOptions>>().Value;

        Assert.Equal("lab-2", options.ChannelName);
        Assert.False(options.Enabled);
        Assert.Equal(AdminChannelProtocol.DefaultName, defaultOptions.ChannelName);
        Assert.True(defaultOptions.Enabled);
        Assert.Contains(registrations, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(AdminChannelServer));   // the server needs the host's ring to start, so it is checked as a registration
        Assert.Contains(registrations, d => d.ServiceType == typeof(AdminChannelHandler));
        Assert.Contains("Wms:Admin:ChannelName", Core.Configuration.BootstrapConfiguration.RecognizedKeys);
        Assert.Contains("Wms:Admin:Enabled", Core.Configuration.BootstrapConfiguration.RecognizedKeys);
        Assert.Throws<ArgumentNullException>(() => AdminChannelServiceCollectionExtensions.AddWmsAdminChannel(null!, defaults));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddWmsAdminChannel(null!));
    }



    [Theory]
    [InlineData("unlock", "unlock", null, null, null)]
    [InlineData("admin unlock --minutes 15 --channel lab --key-ring C:/ring", "unlock", 15, "lab", "C:/ring")]
    [InlineData("lock", "lock", null, null, null)]
    [InlineData("STATUS", "status", null, null, null)]
    public void The_command_line_parses_the_verbs_and_flags(string arguments, string command, int? minutes, string? channel, string? keyRing)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var parsed = AdminCommandLine.Parse(arguments.Split(' '));

        Assert.Null(parsed.Error);
        Assert.Equal(command, parsed.Command);
        Assert.Equal(minutes, parsed.Minutes);
        Assert.Equal(channel, parsed.Channel);
        Assert.Equal(keyRing, parsed.KeyRing);
    }



    [Theory]
    [InlineData("", "Give a command")]
    [InlineData("unlock lock", "Give one command")]
    [InlineData("reboot", "Unknown argument 'reboot'")]
    [InlineData("unlock --minutes", "--minutes needs a value")]
    [InlineData("unlock --minutes 0", "--minutes must be a whole number from 1 to 43200")]
    [InlineData("unlock --minutes 43201", "--minutes must be a whole number from 1 to 43200")]
    [InlineData("unlock --minutes ten", "--minutes must be a whole number from 1 to 43200")]
    [InlineData("lock --minutes 5", "--minutes applies to unlock only")]
    [InlineData("status --channel", "--channel needs a value")]
    public void The_command_line_refuses_what_it_should(string arguments, string expectedError)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var parsed = AdminCommandLine.Parse(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        Assert.NotNull(parsed.Error);
        Assert.Contains(expectedError, parsed.Error, StringComparison.Ordinal);
        Assert.True(AdminCommandLine.Parse(["--help"]).Help);
        Assert.True(AdminCommandLine.Parse(["-h"]).Help);
        Assert.Throws<ArgumentNullException>(() => AdminCommandLine.Parse(null!));
    }



    [Fact]
    public async Task The_tool_explains_usage_errors_a_missing_ring_and_an_unreachable_host()
    {
        var ring = Directory.CreateTempSubdirectory("wms-admin-ring-");
        try
        {
            var configuration = new ConfigurationBuilder().Build();
            using var output = new StringWriter();
            using var error = new StringWriter();

            var help = await AdminProgram.RunAsync(["--help"], output, error, configuration, TimeProvider.System, CancellationToken.None);
            var usage = await AdminProgram.RunAsync(["reboot"], output, error, configuration, TimeProvider.System, CancellationToken.None);
            var noRing = await AdminProgram.RunAsync(["status"], output, error, configuration, TimeProvider.System, CancellationToken.None);
            var missingRing = await AdminProgram.RunAsync(["status", "--key-ring", Path.Combine(ring.FullName, "nope")], output, error, configuration, TimeProvider.System, CancellationToken.None);
            var unreachable = await AdminProgram.RunAsync(["status", "--key-ring", ring.FullName, "--channel", "wms-admin-nobody-" + Guid.NewGuid().ToString("N")], output, error, configuration, TimeProvider.System, CancellationToken.None);

            Assert.Equal(AdminProgram.ExitOk, help);
            Assert.Equal(AdminProgram.ExitUsage, usage);
            Assert.Equal(AdminProgram.ExitUsage, noRing);
            Assert.Equal(AdminProgram.ExitUsage, missingRing);
            Assert.Equal(AdminProgram.ExitUnreachable, unreachable);
            Assert.Contains("wms-admin <command>", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("Unknown argument 'reboot'", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("No key ring directory", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("Key ring directory not found", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("did not answer", error.ToString(), StringComparison.Ordinal);
            Assert.NotEmpty(AdminProgram.OsUser());
            await Assert.ThrowsAsync<ArgumentNullException>(() => AdminProgram.RunAsync(null!, output, error, configuration, TimeProvider.System, CancellationToken.None));
        }
        finally
        {
            ring.Delete(recursive: true);
        }
    }



    private static ServiceProvider Host(ILocalLoginGate gate, bool forceLocal)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [AuthProviderSettings.ForceLocalKey] = forceLocal ? "true" : "false" }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(new AuthProviderCatalog([new LocalAuthProvider()]));
        services.AddSingleton<AuthProviderState>();
        services.AddSingleton<TimeProvider>(new FixedClock(Now));
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddScoped<ILocalLoginGate>(_ => gate);
        services.AddSingleton<AdminChannelHandler>();
        return services.BuildServiceProvider();
    }



    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }



    /// <summary>
    /// A gate that records who asked for what and reports the window it was given.
    /// </summary>
    private sealed class FakeGate : ILocalLoginGate
    {
        public LocalLoginGateInfo Info { get; set; } = LocalLoginGateInfo.Open;

        public List<(string By, TimeSpan Window)> Unlocks { get; } = [];

        public string? LockedBy { get; private set; }



        public Task<LocalLoginGateInfo> GetAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Info);
        }



        public Task<LocalLoginGateInfo> MarkSsoVerifiedAsync(string provider, CancellationToken cancellationToken)
        {
            Info = Info with { SsoVerifiedAt = Now, SsoVerifiedProvider = provider };
            return Task.FromResult(Info);
        }



        public Task<LocalLoginGateInfo> UnlockAsync(TimeSpan window, string openedBy, CancellationToken cancellationToken)
        {
            if (LocalLoginGateRules.Validate(window) is { } reason)
            {
                throw new ArgumentOutOfRangeException(nameof(window), window, reason);
            }

            Unlocks.Add((openedBy, window));
            Info = Info with { UnlockedUntil = Now + window, UnlockedBy = openedBy };
            return Task.FromResult(Info);
        }



        public Task<LocalLoginGateInfo> LockAsync(string closedBy, CancellationToken cancellationToken)
        {
            LockedBy = closedBy;
            Info = Info with { UnlockedUntil = Now };
            return Task.FromResult(Info);
        }
    }
}
