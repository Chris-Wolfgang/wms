// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Wolfgang.Wms.Infrastructure.Database.Conventions;

namespace Wolfgang.Wms.UnitTests.Database;

public sealed class RowVersioningTests
{
    [Fact]
    public void Default_value_draws_the_next_sequence_value_per_provider()
    {
        Assert.Equal("NEXT VALUE FOR [wms].[row_version_seq]", RowVersioning.DefaultValueSql(RowVersioning.SqlServer));
        Assert.Equal("nextval('wms.row_version_seq')", RowVersioning.DefaultValueSql(RowVersioning.PostgreSql));
        Assert.Throws<ArgumentException>(() => RowVersioning.DefaultValueSql("Oracle"));
        Assert.Equal("trg_container_row_version", RowVersioning.TriggerName("container"));
        Assert.Throws<ArgumentException>(() => RowVersioning.TriggerName(" "));
    }



    [Fact]
    public void SqlServer_trigger_is_set_based_and_overwrites_the_supplied_value()
    {
        var sql = RowVersioning.UpdateTriggerSql(RowVersioning.SqlServer, "picking", "container");

        Assert.Contains("CREATE OR ALTER TRIGGER [picking].[trg_container_row_version] ON [picking].[container] AFTER UPDATE", sql, StringComparison.Ordinal);
        Assert.Contains("UPDATE t SET [row_version] = NEXT VALUE FOR [wms].[row_version_seq]", sql, StringComparison.Ordinal);
        Assert.Contains("INNER JOIN inserted i ON t.[id] = i.[id]", sql, StringComparison.Ordinal);
        Assert.Equal("DROP TRIGGER IF EXISTS [picking].[trg_container_row_version];", RowVersioning.DropTriggerSql(RowVersioning.SqlServer, "picking", "container"));
    }



    [Fact]
    public void PostgreSql_trigger_is_per_row_through_a_shared_function()
    {
        var sql = RowVersioning.UpdateTriggerSql(RowVersioning.PostgreSql, "picking", "container");

        Assert.Contains("CREATE OR REPLACE FUNCTION wms.set_row_version() RETURNS trigger", sql, StringComparison.Ordinal);
        Assert.Contains("NEW.row_version := nextval('wms.row_version_seq');", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TRIGGER trg_container_row_version BEFORE UPDATE ON picking.container FOR EACH ROW EXECUTE FUNCTION wms.set_row_version();", sql, StringComparison.Ordinal);
        Assert.Equal("DROP TRIGGER IF EXISTS trg_container_row_version ON picking.container;", RowVersioning.DropTriggerSql(RowVersioning.PostgreSql, "picking", "container"));
    }



    [Fact]
    public void Trigger_sql_rejects_unknown_providers_and_blank_names()
    {
        Assert.Throws<ArgumentException>(() => RowVersioning.UpdateTriggerSql("Oracle", "s", "t"));
        Assert.Throws<ArgumentException>(() => RowVersioning.DropTriggerSql("Oracle", "s", "t"));
        Assert.Throws<ArgumentException>(() => RowVersioning.UpdateTriggerSql(RowVersioning.SqlServer, "", "t"));
        Assert.Throws<ArgumentException>(() => RowVersioning.DropTriggerSql(RowVersioning.SqlServer, "s", ""));
    }



    [Theory]
    [InlineData(RowVersioning.SqlServer)]
    [InlineData(RowVersioning.PostgreSql)]
    public void Migration_helpers_emit_the_trigger_sql_as_operations(string provider)
    {
        var builder = new MigrationBuilder(provider);

        RowVersioning.AddUpdateTrigger(builder, "picking", "container");
        RowVersioning.DropUpdateTrigger(builder, "picking", "container");

        var operations = builder.Operations.OfType<SqlOperation>().Select(o => o.Sql).ToList();
        Assert.Equal(2, operations.Count);
        Assert.Contains("trg_container_row_version", operations[0], StringComparison.Ordinal);
        Assert.StartsWith("DROP TRIGGER IF EXISTS", operations[1], StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => RowVersioning.AddUpdateTrigger(null!, "s", "t"));
        Assert.Throws<ArgumentNullException>(() => RowVersioning.DropUpdateTrigger(null!, "s", "t"));
    }
}
