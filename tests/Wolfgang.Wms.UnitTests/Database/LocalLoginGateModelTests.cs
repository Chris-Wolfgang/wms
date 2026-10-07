// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Identity.BreakGlass;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Identity;

namespace Wolfgang.Wms.UnitTests.Database;

/// <summary>
/// E9.3 on the product model for each provider: <c>core.local_login_gate</c> with its columns, the
/// row-version index and trigger of a versioned table, and the row's view of itself.
/// </summary>
public sealed class LocalLoginGateModelTests
{
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void Core_local_login_gate_is_a_versioned_singleton_table(string provider)
    {
        using var context = new WmsDbContext(ModelConventionsTests.Options<WmsDbContext>(provider));
        var entity = context.Model.FindEntityType(typeof(LocalLoginGate))!;

        Assert.Equal("local_login_gate", entity.GetTableName());
        Assert.Equal("core", entity.GetSchema());
        Assert.Equal
        (
            ["id", "locked_at", "locked_by", "row_version", "sso_verified_at", "sso_verified_provider", "unlocked_at", "unlocked_by", "unlocked_until"],
            entity.GetProperties().Select(p => p.GetColumnName()).Order(StringComparer.Ordinal)
        );
        Assert.Equal(LocalLoginGate.ProviderLength, entity.FindProperty(nameof(LocalLoginGate.SsoVerifiedProvider))!.GetMaxLength());
        Assert.Equal(LocalLoginGate.ActorLength, entity.FindProperty(nameof(LocalLoginGate.UnlockedBy))!.GetMaxLength());
        Assert.Equal(LocalLoginGate.ActorLength, entity.FindProperty(nameof(LocalLoginGate.LockedBy))!.GetMaxLength());
        Assert.True(entity.FindProperty(nameof(LocalLoginGate.SsoVerifiedAt))!.IsNullable);
        Assert.Equal(["ix_local_login_gate_row_version"], entity.GetIndexes().Select(i => i.GetDatabaseName()));
        Assert.Equal(["trg_local_login_gate_row_version"], entity.GetDeclaredTriggers().Select(t => t.ModelName));
        Assert.Throws<ArgumentNullException>(() => new LocalLoginGateConfiguration().Configure(null!));
    }



    [Fact]
    public void The_row_reports_itself_as_the_gate_info()
    {
        var at = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
        var row = new LocalLoginGate { SsoVerifiedAt = at, SsoVerifiedProvider = "oidc", UnlockedUntil = at.AddMinutes(30), UnlockedBy = "HOST\\ops", UnlockedAt = at, LockedAt = null, LockedBy = null };

        Assert.Equal(new LocalLoginGateInfo(at, "oidc", at.AddMinutes(30), "HOST\\ops"), row.ToInfo());
        Assert.Equal(LocalLoginGateInfo.Open, new LocalLoginGate().ToInfo());
    }
}
