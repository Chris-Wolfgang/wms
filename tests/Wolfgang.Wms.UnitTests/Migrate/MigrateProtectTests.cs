// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.Configuration;
using Wolfgang.Wms.Migrate;

namespace Wolfgang.Wms.UnitTests.Migrate;

/// <summary>
/// E8.2: <c>wms-migrate --protect</c> encrypts the connection string with the key ring and refuses to run
/// without one, without a string, or on a string that is already encrypted; the flags parse and cannot be
/// combined with the other modes.
/// </summary>
public sealed class MigrateProtectTests
{
    [Fact]
    public async Task Protect_prints_the_encrypted_string_and_the_ring_decrypts_it_again()
    {
        var ring = Path.Combine(Path.GetTempPath(), "wms-protect-" + Guid.NewGuid().ToString("N"));
        var output = new StringWriter();
        var error = new StringWriter();
        try
        {
            var exit = await MigrateProgram.RunAsync(["--protect", "--connection-string", "Host=db;Password=hunter2", "--key-ring", ring], output, error, Configuration(), CancellationToken.None);
            var encrypted = output.ToString().Trim();
            var byConfiguration = await MigrateProgram.RunAsync(["--protect", "--connection-string", "Host=db"], output, error, Configuration(("Wms:DataProtection:KeyRingPath", ring)), CancellationToken.None);

            Assert.Equal(MigrateProgram.ExitOk, exit);
            Assert.StartsWith("enc:v1:", encrypted, StringComparison.Ordinal);
            Assert.DoesNotContain("hunter2", encrypted, StringComparison.Ordinal);
            Assert.Equal("Host=db;Password=hunter2", Wolfgang.Wms.Infrastructure.Secrets.KeyRing.CreateProtector(ring).Unprotect(encrypted));
            Assert.Equal(MigrateProgram.ExitOk, byConfiguration);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            if (Directory.Exists(ring))
            {
                Directory.Delete(ring, recursive: true);
            }
        }
    }



    [Fact]
    public async Task Protect_refuses_a_missing_string_an_encrypted_string_and_a_missing_ring()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var noString = await MigrateProgram.RunAsync(["--protect"], output, error, Configuration(), CancellationToken.None);
        var already = await MigrateProgram.RunAsync(["--protect", "--connection-string", "enc:v1:abc"], output, error, Configuration(), CancellationToken.None);
        var noRing = await MigrateProgram.RunAsync(["--protect", "--connection-string", "Host=db"], output, error, Configuration(), CancellationToken.None);

        Assert.Equal(MigrateProgram.ExitUsage, noString);
        Assert.Equal(MigrateProgram.ExitUsage, already);
        Assert.Equal(MigrateProgram.ExitUsage, noRing);
        Assert.Contains("needs a connection string", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("already encrypted", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("Wms:DataProtection:KeyRingPath", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, output.ToString());
    }



    [Fact]
    public void Flags_parse_and_cannot_be_combined_with_the_other_modes()
    {
        var parsed = MigrateCommandLine.Parse(["--protect", "--key-ring", "/keys"]);

        Assert.True(parsed.Protect);
        Assert.Equal("/keys", parsed.KeyRing);
        Assert.Null(parsed.Error);
        Assert.Equal("--status, --script and --protect cannot be combined.", MigrateCommandLine.Parse(["--protect", "--status"]).Error);
        Assert.Equal("--key-ring needs a value.", MigrateCommandLine.Parse(["--key-ring"]).Error);
        Assert.Contains("--protect", MigrateCommandLine.Usage, StringComparison.Ordinal);
    }



    private static IConfiguration Configuration(params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value, StringComparer.Ordinal))
            .Build();
    }
}
