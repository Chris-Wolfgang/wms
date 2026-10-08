// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Zones;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Zones;
using Wolfgang.Wms.UnitTests.Database;

namespace Wolfgang.Wms.UnitTests.Zones;

public sealed class ZonesUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly ZoneDraft Pick = new("A01", "Aisle 1", ZoneType.Pick, "A", IsRejectLane: true, Resolution: null);
    private static readonly ResolutionZone Props = new("RESTOCK-01", "RET-BIN", [7, 3], AcceptsWeightFailures: true, AcceptsShorts: true, AcceptsAdjustments: false, AcceptsMisdirects: true, IsVirtualQueue: false);
    private static readonly ZoneDraft Resolution = new("RES-1", "Resolution lane", ZoneType.Resolution, null, IsRejectLane: false, Props);



    [Fact]
    public void Pick_bulk_and_resolution_drafts_are_valid()
    {
        Assert.Null(ZoneRules.Validate(Pick));
        Assert.Null(ZoneRules.Validate(new ZoneDraft(" b_1 ", "Bulk", ZoneType.Bulk, null, IsRejectLane: false, Resolution: null, IsActive: false)));
        Assert.Null(ZoneRules.Validate(Resolution));
        Assert.Null(ZoneRules.Validate(Resolution with { Resolution = Props with { RestockingBin = null, ReturnsContainer = " ", ResolverUserIds = [] } }));
        Assert.Equal("RES-1", ZoneRules.Normalize(" res-1 "));
        Assert.Throws<ArgumentNullException>(() => ZoneRules.Validate(null!));
        Assert.Throws<ArgumentNullException>(() => ZoneRules.Normalize(null!));
    }



    public static TheoryData<ZoneDraft, string> InvalidDrafts => new()
    {
        { Pick with { Code = "" }, "code is required" },
        { Pick with { Code = new string('x', 33) }, "code must be at most 32" },
        { Pick with { Code = "A 01" }, "code may contain letters, digits, '-' and '_' only" },
        { Pick with { Name = " " }, "name is required" },
        { Pick with { Name = new string('x', 129) }, "name must be at most 128" },
        { Pick with { WalkOrderPrefix = new string('A', 17) }, "walkOrderPrefix must be at most 16" },
        { Pick with { Type = (ZoneType)9 }, "type must be Pick, Bulk or Resolution" },
        { Pick with { Type = ZoneType.Bulk }, "isRejectLane applies to pick zones only" },
        { Pick with { IsRejectLane = false, Resolution = Props }, "resolution applies to resolution zones only" },
        { Resolution with { Resolution = null }, "resolution is required for a resolution zone" },
        { Resolution with { Resolution = Props with { RestockingBin = new string('x', 65) } }, "resolution.restockingBin must be at most 64" },
        { Resolution with { Resolution = Props with { ReturnsContainer = new string('x', 65) } }, "resolution.returnsContainer must be at most 64" },
        { Resolution with { Resolution = Props with { ResolverUserIds = null! } }, "resolution.resolverUserIds is required" },
        { Resolution with { Resolution = Props with { ResolverUserIds = Enumerable.Range(1, 65).Select(i => (long)i).ToList() } }, "at most 64 users" },
        { Resolution with { Resolution = Props with { ResolverUserIds = [0] } }, "must be user ids" },
        { Resolution with { Resolution = Props with { ResolverUserIds = [7, 7] } }, "must not repeat a user" },
    };



    [Theory]
    [MemberData(nameof(InvalidDrafts))]
    public void An_invalid_draft_names_the_first_problem(ZoneDraft draft, string expectedFragment)
    {
        var reason = ZoneRules.Validate(draft);

        Assert.NotNull(reason);
        Assert.Contains(expectedFragment, reason, StringComparison.Ordinal);
    }



    [Fact]
    public void The_row_round_trips_a_resolution_zone_and_replaces_its_resolvers()
    {
        var row = new Zone { SiteId = 5 };

        row.Apply(Resolution with { Code = " res-1 ", WalkOrderPrefix = " " }, Now, "admin");
        var first = row.ToInfo();
        row.Apply(Resolution with { Resolution = Props with { ResolverUserIds = [3, 11], IsVirtualQueue = true } }, Now, "admin");
        var second = row.ToInfo();
        row.Apply(Pick, Now, "admin");
        var pick = row.ToInfo();

        Assert.Equal(("res-1", 5L, ZoneType.Resolution, (string?)null), (first.Code, first.SiteId, first.Type, first.WalkOrderPrefix));
        Assert.Equal("A01", row.CodeNormalized);
        Assert.Equal([3L, 7L], first.Resolution!.ResolverUserIds);
        Assert.Equal(("RESTOCK-01", "RET-BIN", true, true, false, true, false), (first.Resolution.RestockingBin, first.Resolution.ReturnsContainer, first.Resolution.AcceptsWeightFailures, first.Resolution.AcceptsShorts, first.Resolution.AcceptsAdjustments, first.Resolution.AcceptsMisdirects, first.Resolution.IsVirtualQueue));
        Assert.Equal([3L, 11L], second.Resolution!.ResolverUserIds);
        Assert.True(second.Resolution.IsVirtualQueue);
        Assert.Null(pick.Resolution);
        Assert.Equal(("A01", ZoneType.Pick, "A", true), (pick.Code, pick.Type, pick.WalkOrderPrefix, pick.IsRejectLane));
        Assert.Empty(row.Resolvers);
        Assert.Equal((Now, "admin", "\"0\""), (pick.UpdatedAt, pick.UpdatedBy, pick.Etag));
        Assert.Throws<ArgumentNullException>(() => row.Apply(null!, Now, "admin"));
    }



    [Fact]
    public async Task The_placeholders_answer_unavailable_and_no_open_groups()
    {
        var store = new NoZones();
        var groups = new NoOpenZoneGroups();

        var list = await Assert.ThrowsAsync<ZoneException>(() => store.ListAsync(1, CancellationToken.None));
        var find = await Assert.ThrowsAsync<ZoneException>(() => store.FindAsync(1, 1, CancellationToken.None));
        var create = await Assert.ThrowsAsync<ZoneException>(() => store.CreateAsync(1, Pick, "admin", CancellationToken.None));
        var update = await Assert.ThrowsAsync<ZoneException>(() => store.UpdateAsync(1, 1, Pick, "admin", CancellationToken.None));

        Assert.All([list, find, create, update], e => Assert.Equal(ZoneErrorCodes.Unavailable, e.Code));
        Assert.Equal(0, await groups.CountOpenAsync(1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.CreateAsync(1, null!, "admin", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdateAsync(1, 1, Pick, " ", CancellationToken.None));
        Assert.Equal(ZoneErrorCodes.Unavailable, new ZoneException().Code);
        Assert.Equal("boom", new ZoneException("boom").Message);
        Assert.Equal("cause", new ZoneException("boom", new InvalidOperationException("cause")).InnerException!.Message);
        Assert.Equal(ZoneErrorCodes.Invalid, new ZoneException(ZoneErrorCodes.Invalid, "x", new InvalidOperationException()).Code);
        Assert.Throws<ArgumentNullException>(() => new ZoneException(null!, "x"));
    }



    [Fact]
    public void The_module_declares_its_permissions_and_codes()
    {
        Assert.Equal("zones", ZonesModule.Descriptor.Name);
        Assert.Equal(["zones.read", "zones.write"], ZonesModule.Descriptor.Permissions.Select(p => p.Name));
        Assert.Equal([BuiltInRole.Supervisor, BuiltInRole.Support, BuiltInRole.Viewer], ZonesModule.Read.DefaultRoles);
        Assert.Empty(ZonesModule.Write.DefaultRoles);
        Assert.Equal(["zones.site_not_found", "zones.not_found", "zones.code_taken", "zones.invalid", "zones.has_open_groups", "zones.unavailable"], ZonesModule.Descriptor.ErrorCodes.Select(c => c.Code));
        Assert.Throws<ArgumentNullException>(() => ZonesModule.AddWmsZonesModule(null!));
    }



    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void Core_zone_is_a_versioned_table_keyed_by_site_and_code_with_a_resolver_table(string provider)
    {
        using var context = new WmsDbContext(ModelConventionsTests.Options<WmsDbContext>(provider));
        var zone = context.Model.FindEntityType(typeof(Zone))!;
        var resolver = context.Model.FindEntityType(typeof(ZoneResolver))!;
        var type = zone.FindProperty(nameof(Zone.Type))!;

        Assert.Equal(("zone", "layout"), (zone.GetTableName(), zone.GetSchema()));
        Assert.Equal(("zone_resolver", "layout"), (resolver.GetTableName(), resolver.GetSchema()));
        Assert.Equal(ZoneConfiguration.TypeLength, type.GetMaxLength());
        Assert.Equal("resolution", type.GetValueConverter()!.ConvertToProvider(ZoneType.Resolution));
        Assert.Equal(ZoneType.Pick, type.GetValueConverter()!.ConvertFromProvider("PICK"));
        Assert.Contains(zone.GetIndexes(), i => i.IsUnique && string.Equals(i.GetDatabaseName(), "ux_zone_site_id_code_normalized", StringComparison.Ordinal));
        Assert.Contains(zone.GetIndexes(), i => string.Equals(i.GetDatabaseName(), "ix_zone_row_version", StringComparison.Ordinal));
        Assert.Equal(["trg_zone_row_version"], zone.GetDeclaredTriggers().Select(t => t.ModelName));
        Assert.Contains(zone.GetForeignKeys(), fk => string.Equals(fk.PrincipalEntityType.ClrType.Name, "Site", StringComparison.Ordinal));
        Assert.Contains(resolver.GetIndexes(), i => i.IsUnique && string.Equals(i.GetDatabaseName(), "ux_zone_resolver_zone_id_user_id", StringComparison.Ordinal));
        Assert.Equal(2, resolver.GetForeignKeys().Count());
        Assert.Throws<ArgumentNullException>(() => new ZoneConfiguration().Configure((Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Zone>)null!));
        Assert.Throws<ArgumentNullException>(() => new ZoneConfiguration().Configure((Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ZoneResolver>)null!));
    }
}
