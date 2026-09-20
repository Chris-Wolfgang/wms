// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Database.Conventions;
using Wolfgang.Wms.Infrastructure.Secrets;
using Wolfgang.Wms.UnitTests.Database;

namespace Wolfgang.Wms.UnitTests.Secrets;

/// <summary>
/// E8.6 without a database: the key table is part of the product model in <c>wms</c> with snake_case names,
/// and the ring goes to the database when a provider is configured and no path is, to a directory when a
/// path is, and to the framework default with neither.
/// </summary>
public sealed class DatabaseKeyRingTests
{
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void Key_table_is_mapped_into_wms_with_snake_case_names(string provider)
    {
        using var context = new WmsDbContext(ModelConventionsTests.Options<WmsDbContext>(provider));
        var entity = context.Model.FindEntityType(typeof(DataProtectionKey))!;

        Assert.Equal(("wms", "data_protection_key"), (entity.GetSchema(), entity.GetTableName()));
        Assert.Equal(["friendly_name", "id", "xml"], entity.GetProperties().Select(p => p.GetColumnName()).Order(StringComparer.Ordinal));
        Assert.Equal(256, entity.FindProperty(nameof(DataProtectionKey.FriendlyName))!.GetMaxLength());
        Assert.True(ModelConventions.IsLibraryOwned(typeof(DataProtectionKey)));
        Assert.Empty(ModelConventions.Verify(context.Model));
        Assert.NotNull(context.DataProtectionKeys);
    }



    [Theory]
    [InlineData("SqlServer", null, "database")]
    [InlineData("PostgreSql", null, "database")]
    [InlineData("SqlServer", "ring", "file")]
    [InlineData("None", null, "default")]
    public void The_ring_lives_in_the_database_unless_a_path_is_configured(string provider, string? path, string expected)
    {
        var ring = path is null ? null : Path.Combine(Path.GetTempPath(), "wms-" + path + "-" + Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Wms:Database:Provider"] = provider,
            ["Wms:Database:ConnectionString"] = "Host=db",
            [KeyRingOptions.PathKey] = ring,
        }).Build();
        var services = new ServiceCollection();
        services.AddWmsDataProtection(configuration);
        services.AddDbContext<WmsDbContext>(o => o.UseNpgsql("Host=db"));
        try
        {
            using var provider1 = services.BuildServiceProvider();
            var repository = provider1.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository;

            var actual = repository switch
            {
                EntityFrameworkCoreXmlRepository<WmsDbContext> => "database",
                null => "default",
                _ => "file",
            };
            Assert.Equal(expected, actual);
        }
        finally
        {
            if (ring is not null && Directory.Exists(ring))
            {
                Directory.Delete(ring, recursive: true);
            }
        }
    }
}
