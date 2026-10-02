// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Secrets;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Secrets;

namespace Wolfgang.Wms.UnitTests.Secrets;

/// <summary>
/// E8.1/E8.5: the Data Protection protector round-trips, refuses foreign ciphertext with a message naming
/// the key to fix, the file ring is created on first run and shared by hosts and tools, and the encrypted
/// connection string decrypts through the options.
/// </summary>
public sealed class DataProtectionTests
{
    [Fact]
    public void Protector_round_trips_and_names_the_key_ring_when_the_key_is_missing()
    {
        var mine = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());
        var other = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());

        var protectedText = mine.Protect("Server=db;Password=hunter2");

        Assert.StartsWith("enc:v1:", protectedText, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", protectedText, StringComparison.Ordinal);
        Assert.Equal("Server=db;Password=hunter2", mine.Unprotect(protectedText));
        var foreign = Assert.Throws<InvalidOperationException>(() => other.Unprotect(protectedText));
        Assert.Contains("Wms:DataProtection:KeyRingPath", foreign.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", foreign.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => mine.Unprotect("Server=db"));
        Assert.Throws<ArgumentNullException>(() => mine.Protect(null!));
        Assert.Throws<ArgumentNullException>(() => new DataProtectionSecretProtector(null!));
    }



    [Fact]
    public void File_ring_is_created_on_first_run_and_shared_by_the_host_and_the_tool()
    {
        var path = Path.Combine(Path.GetTempPath(), "wms-keys-" + Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [KeyRingOptions.PathKey] = path }).Build();
            var services = new ServiceCollection();
            services.AddWmsDataProtection(configuration);
            using var provider = services.BuildServiceProvider();

            var host = provider.GetRequiredService<ISecretProtector>();
            var protectedText = host.Protect("secret");
            var tool = KeyRing.TryCreateProtector(configuration)!;

            Assert.True(Directory.Exists(path));
            Assert.NotEmpty(Directory.GetFiles(path, "key-*.xml"));
            Assert.Equal("secret", tool.Unprotect(protectedText));
            Assert.Equal(path, provider.GetRequiredService<IOptions<KeyRingOptions>>().Value.KeyRingPath);
            Assert.True(provider.GetRequiredService<IOptions<KeyRingOptions>>().Value.UsesFileSystem);
            Assert.Same(KeyRing.EnsureDirectory(path).FullName, KeyRing.EnsureDirectory(path).FullName);   // second call: already there
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }



    [Fact]
    public void Without_a_path_the_framework_ring_is_used_and_the_tool_has_no_protector()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddWmsDataProtection(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.IsType<DataProtectionSecretProtector>(provider.GetRequiredService<ISecretProtector>());
        Assert.False(provider.GetRequiredService<IOptions<KeyRingOptions>>().Value.UsesFileSystem);
        Assert.Null(KeyRing.TryCreateProtector(configuration));
        Assert.Throws<ArgumentNullException>(() => KeyRing.TryCreateProtector(null!));
        Assert.Throws<ArgumentException>(() => KeyRing.EnsureDirectory(" "));
        Assert.Throws<ArgumentException>(() => KeyRing.CreateProtector(""));
        Assert.Throws<ArgumentNullException>(() => SecretsServiceCollectionExtensions.AddWmsDataProtection(null!, configuration));
        Assert.Throws<ArgumentNullException>(() => services.AddWmsDataProtection(null!));
    }



    [Fact]
    public void An_encrypted_connection_string_decrypts_through_the_options_and_needs_a_protector()
    {
        var protector = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());
        var options = new DatabaseOptions { Provider = "SqlServer", ConnectionString = protector.Protect("Server=db;Database=wms"), TrustServerCertificate = true };
        var plain = new DatabaseOptions { Provider = "PostgreSql", ConnectionString = "Host=db" };

        Assert.True(options.ConnectionStringIsProtected);
        Assert.False(plain.ConnectionStringIsProtected);
        var effective = new SqlConnectionStringBuilder(options.EffectiveConnectionString(protector));   // the builder normalises keyword spelling
        Assert.Equal("db", effective.DataSource);
        Assert.Equal("wms", effective.InitialCatalog);
        Assert.True(effective.TrustServerCertificate);
        Assert.Equal("Host=db", plain.EffectiveConnectionString());
        Assert.Throws<InvalidOperationException>(() => options.EffectiveConnectionString());
        Assert.Empty(options.Validate());
    }



    [Fact]
    public async Task Startup_check_passes_plain_strings_refuses_missing_paths_and_wrong_rings()
    {
        var ring = new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());
        var encrypted = ring.Protect("Host=db");
        using var withRing = new ServiceCollection().AddSingleton<ISecretProtector>(ring).BuildServiceProvider();
        using var wrongRing = new ServiceCollection().AddSingleton<ISecretProtector>(new DataProtectionSecretProtector(new EphemeralDataProtectionProvider())).BuildServiceProvider();

        await new SecretsStartupCheck(withRing, Options.Create(new DatabaseOptions { ConnectionString = "Host=db" }), Options.Create(new KeyRingOptions())).StartAsync(CancellationToken.None);
        await new SecretsStartupCheck(withRing, Options.Create(new DatabaseOptions { ConnectionString = encrypted }), Options.Create(new KeyRingOptions { KeyRingPath = "/keys" })).StartAsync(CancellationToken.None);
        await new SecretsStartupCheck(withRing, Options.Create(new DatabaseOptions()), Options.Create(new KeyRingOptions())).StopAsync(CancellationToken.None);
        var noPath = await Assert.ThrowsAsync<InvalidOperationException>(() => new SecretsStartupCheck(withRing, Options.Create(new DatabaseOptions { ConnectionString = encrypted }), Options.Create(new KeyRingOptions())).StartAsync(CancellationToken.None));
        var wrongKey = await Assert.ThrowsAsync<InvalidOperationException>(() => new SecretsStartupCheck(wrongRing, Options.Create(new DatabaseOptions { ConnectionString = encrypted }), Options.Create(new KeyRingOptions { KeyRingPath = "/keys" })).StartAsync(CancellationToken.None));

        Assert.Contains("KeyRingPath is not set", noPath.Message, StringComparison.Ordinal);
        Assert.Contains("does not hold the key", wrongKey.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => new SecretsStartupCheck(null!, Options.Create(new DatabaseOptions()), Options.Create(new KeyRingOptions())));
        Assert.Throws<ArgumentNullException>(() => new SecretsStartupCheck(withRing, null!, Options.Create(new KeyRingOptions())));
        Assert.Throws<ArgumentNullException>(() => new SecretsStartupCheck(withRing, Options.Create(new DatabaseOptions()), null!));
    }
}
