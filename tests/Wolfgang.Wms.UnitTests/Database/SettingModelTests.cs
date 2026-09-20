// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Database.Settings;

namespace Wolfgang.Wms.UnitTests.Database;

/// <summary>
/// E6.2 on the product model for each provider: <c>core.setting</c> with the required columns, the unique
/// (scope_type, scope_id, key) index, the row-version trigger declared and the soft-delete filter.
/// </summary>
public sealed class SettingModelTests
{
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void Core_setting_has_the_columns_indexes_trigger_and_filter_of_a_synced_table(string provider)
    {
        using var context = new WmsDbContext(ModelConventionsTests.Options<WmsDbContext>(provider));
        var entity = context.Model.FindEntityType(typeof(Setting))!;

        Assert.Equal("setting", entity.GetTableName());
        Assert.Equal("core", entity.GetSchema());
        Assert.Equal
        (
            ["cascade_mode", "configured_value", "deleted_at", "effective_value", "id", "key", "row_version", "scope_id", "scope_type", "updated_at", "updated_by"],
            entity.GetProperties().Select(p => p.GetColumnName()).Order(StringComparer.Ordinal)
        );
        Assert.Equal(Setting.ScopeTypeLength, entity.FindProperty(nameof(Setting.ScopeType))!.GetMaxLength());
        Assert.Equal(Setting.KeyLength, entity.FindProperty(nameof(Setting.Key))!.GetMaxLength());
        Assert.Equal(Setting.UpdatedByLength, entity.FindProperty(nameof(Setting.UpdatedBy))!.GetMaxLength());
        Assert.Equal("value", entity.FindProperty(nameof(Setting.CascadeMode))!.GetDefaultValue());
        Assert.False(entity.FindProperty(nameof(Setting.EffectiveValue))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(Setting.ConfiguredValue))!.IsNullable);
        Assert.Equal(["ix_setting_row_version", "ux_setting_scope_type_scope_id_key"], entity.GetIndexes().Select(i => i.GetDatabaseName()).Order(StringComparer.Ordinal));
        Assert.True(entity.GetIndexes().Single(i => i.Properties.Count == 3).IsUnique);
        Assert.Equal(["trg_setting_row_version"], entity.GetDeclaredTriggers().Select(t => t.ModelName));
        Assert.Single(entity.GetDeclaredQueryFilters());
        Assert.Throws<ArgumentNullException>(() => new SettingConfiguration().Configure(null!));
    }
}
