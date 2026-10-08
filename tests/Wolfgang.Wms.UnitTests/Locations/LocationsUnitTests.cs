// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Http.Paging;
using Wolfgang.Wms.Core.Locations;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Locations;
using Wolfgang.Wms.UnitTests.Database;

namespace Wolfgang.Wms.UnitTests.Locations;

public sealed class LocationsUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly LocationDraft Valid = new("A-01-02-03", "LOC000123", 7, "A-0102-03");



    [Fact]
    public void A_draft_with_a_code_a_barcode_a_zone_and_a_walk_sequence_is_valid()
    {
        Assert.Null(LocationRules.Validate(Valid));
        Assert.Null(LocationRules.Validate(new LocationDraft(" b_1 ", " 4006381333931 ", 1, " B-0001 ", IsPickable: false, IsActive: false)));
        Assert.Equal("A-01", LocationRules.Normalize(" a-01 "));
        Assert.Null(LocationRules.WalkSequenceUnderPrefix("A-0101", null));
        Assert.Null(LocationRules.WalkSequenceUnderPrefix("a-0101", "A"));
        Assert.Contains("must start with the zone's walk-order prefix 'A'", LocationRules.WalkSequenceUnderPrefix("B-0101", "A"), StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => LocationRules.Validate(null!));
        Assert.Throws<ArgumentNullException>(() => LocationRules.Normalize(null!));
    }



    public static TheoryData<LocationDraft, string> InvalidDrafts => new()
    {
        { Valid with { Code = "" }, "code is required" },
        { Valid with { Code = new string('x', 65) }, "code must be at most 64" },
        { Valid with { Code = "A 01" }, "code may contain letters, digits, '-' and '_' only" },
        { Valid with { Barcode = " " }, "barcode is required" },
        { Valid with { Barcode = new string('1', 129) }, "barcode must be at most 128" },
        { Valid with { Barcode = "LOC 1" }, "barcode may contain visible ASCII characters only" },
        { Valid with { Barcode = "LOC|1" }, "barcode may contain visible ASCII characters only" },
        { Valid with { ZoneId = 0 }, "zoneId is required" },
        { Valid with { WalkSequence = "" }, "walkSequence is required" },
        { Valid with { WalkSequence = new string('A', 65) }, "walkSequence must be at most 64" },
        { Valid with { WalkSequence = "A 01" }, "walkSequence may contain visible ASCII characters only" },
    };



    [Theory]
    [MemberData(nameof(InvalidDrafts))]
    public void An_invalid_draft_names_the_first_problem(LocationDraft draft, string expectedFragment)
    {
        var reason = LocationRules.Validate(draft);

        Assert.NotNull(reason);
        Assert.Contains(expectedFragment, reason, StringComparison.Ordinal);
    }



    [Fact]
    public void The_row_round_trips_a_draft_and_normalizes_the_code()
    {
        var row = new Location { SiteId = 5 };

        row.Apply(new LocationDraft(" a-01-02-03 ", " LOC000123 ", 7, " A-0102-03 ", IsPickable: false), Now, "admin");
        var info = row.ToInfo();

        Assert.Equal(("a-01-02-03", "A-01-02-03", "LOC000123", "A-0102-03"), (row.Code, row.CodeNormalized, row.Barcode, row.WalkSequence));
        Assert.Equal(new LocationInfo(0, 5, 7, "a-01-02-03", "LOC000123", "A-0102-03", false, true, Now, "admin", 0), info);
        Assert.Equal("\"0\"", info.Etag);
        Assert.Throws<ArgumentNullException>(() => row.Apply(null!, Now, "admin"));
    }



    [Fact]
    public async Task The_placeholder_answers_unavailable_and_the_exception_carries_its_code()
    {
        var store = new NoLocations();
        var query = new LocationQuery(null, null, null, new PageQuery(PageDirection.Forward, default, LocationsModule.Sorting.Default, 50));

        var list = await Assert.ThrowsAsync<LocationException>(() => store.ListAsync(1, query, CancellationToken.None));
        var find = await Assert.ThrowsAsync<LocationException>(() => store.FindAsync(1, 1, CancellationToken.None));
        var create = await Assert.ThrowsAsync<LocationException>(() => store.CreateAsync(1, Valid, "admin", CancellationToken.None));
        var update = await Assert.ThrowsAsync<LocationException>(() => store.UpdateAsync(1, 1, Valid, "admin", CancellationToken.None));

        Assert.All([list, find, create, update], e => Assert.Equal(LocationErrorCodes.Unavailable, e.Code));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.ListAsync(1, null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.CreateAsync(1, null!, "admin", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdateAsync(1, 1, Valid, " ", CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => new LocationQuery(null, null, null, null!));
        Assert.Equal(LocationErrorCodes.Unavailable, new LocationException().Code);
        Assert.Equal("boom", new LocationException("boom").Message);
        Assert.Equal("cause", new LocationException("boom", new InvalidOperationException("cause")).InnerException!.Message);
        Assert.Equal(LocationErrorCodes.Invalid, new LocationException(LocationErrorCodes.Invalid, "x", new InvalidOperationException()).Code);
        Assert.Throws<ArgumentNullException>(() => new LocationException(null!, "x"));
    }



    [Fact]
    public void The_module_declares_its_permissions_codes_and_sorts()
    {
        Assert.Equal("locations", LocationsModule.Descriptor.Name);
        Assert.Equal(["locations.read", "locations.write"], LocationsModule.Descriptor.Permissions.Select(p => p.Name));
        Assert.Equal([BuiltInRole.Supervisor, BuiltInRole.Support, BuiltInRole.Viewer], LocationsModule.Read.DefaultRoles);
        Assert.Empty(LocationsModule.Write.DefaultRoles);
        Assert.Equal(["locations.site_not_found", "locations.not_found", "locations.zone_not_found", "locations.code_taken", "locations.barcode_taken", "locations.invalid", "locations.unavailable"], LocationsModule.Descriptor.ErrorCodes.Select(c => c.Code));
        Assert.Equal(SortOrder.Ascending("walk_sequence"), LocationsModule.Sorting.Default);
        Assert.Equal(["barcode", "code", "id", "walk_sequence"], LocationsModule.Sorting.Fields);
        Assert.Throws<ArgumentNullException>(() => LocationsModule.AddWmsLocationsModule(null!));
    }



    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void Core_location_is_a_versioned_table_with_unique_code_and_barcode_per_site_and_keyset_indexes(string provider)
    {
        using var context = new WmsDbContext(ModelConventionsTests.Options<WmsDbContext>(provider));
        var entity = context.Model.FindEntityType(typeof(Location))!;
        var indexes = entity.GetIndexes().Select(i => (i.GetDatabaseName(), i.IsUnique)).ToList();

        Assert.Equal(("location", "layout"), (entity.GetTableName(), entity.GetSchema()));
        Assert.Equal(LocationRules.BarcodeLength, entity.FindProperty(nameof(Location.Barcode))!.GetMaxLength());
        Assert.Contains(("ux_location_site_id_code_normalized", true), indexes);
        Assert.Contains(("ux_location_site_id_barcode", true), indexes);
        Assert.Contains(("ix_location_site_id_walk_sequence_id", false), indexes);
        Assert.Contains(("ix_location_zone_id_walk_sequence_id", false), indexes);
        Assert.Contains(("ix_location_row_version", false), indexes);
        Assert.Equal(["trg_location_row_version"], entity.GetDeclaredTriggers().Select(t => t.ModelName));
        Assert.Equal(["Site", "Zone"], entity.GetForeignKeys().Select(fk => fk.PrincipalEntityType.ClrType.Name).Order(StringComparer.Ordinal));
        Assert.Throws<ArgumentNullException>(() => new LocationConfiguration().Configure(null!));
    }
}
