// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Json;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Core.Schema;

namespace Wolfgang.Wms.UnitTests.Schema;

public sealed class SchemaStatusTests
{
    [Theory]
    [InlineData("m1", "m1", true)]
    [InlineData("m1", "m2", false)]
    [InlineData(null, "m1", false)]
    [InlineData(null, null, false)]
    public void UpToDate_only_when_current_equals_expected_and_both_exist(string? current, string? expected, bool upToDate)
    {
        Assert.Equal(upToDate, new SchemaStatus(current, expected).UpToDate);
    }



    [Fact]
    public async Task The_placeholder_source_reports_no_database()
    {
        var status = await new NotInstalledSchemaVersionSource().GetAsync(CancellationToken.None);

        Assert.Null(status.Current);
        Assert.Null(status.Expected);
        Assert.False(status.UpToDate);
    }



    [Fact]
    public void SchemaStatus_serialises_camelCase_through_the_source_generated_context()
    {
        var json = JsonSerializer.Serialize(new SchemaStatus("m1", "m2"), WmsJsonContext.Default.SchemaStatus);

        Assert.Equal("{\"current\":\"m1\",\"expected\":\"m2\",\"upToDate\":false}", json);
    }



    [Fact]
    public void AddWmsSchemaModule_registers_the_placeholder_source_and_the_system_module()
    {
        var services = new ServiceCollection();
        services.AddWmsModules();
        services.AddWmsSchemaModule();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<NotInstalledSchemaVersionSource>(provider.GetRequiredService<ISchemaVersionSource>());
        Assert.Contains(provider.GetRequiredService<ModuleCollection>().Modules, m => string.Equals(m.Name, "system", StringComparison.Ordinal));
        Assert.Equal("/system/schema", SchemaModule.Route);
        Assert.Throws<ArgumentNullException>(() => SchemaModule.AddWmsSchemaModule(null!));
    }
}
