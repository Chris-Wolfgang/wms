// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Copies;
using Wolfgang.Wms.Core.Locations;
using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Core.Zones;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Locations;
using Wolfgang.Wms.Infrastructure.Sites;
using Wolfgang.Wms.Infrastructure.Zones;
using Wolfgang.Wms.UnitTests.Database;

namespace Wolfgang.Wms.UnitTests.Copies;

public sealed class CopiesUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);



    [Fact]
    public void A_substitution_needs_both_halves_or_neither_and_replaces_the_prefix_without_regard_to_case()
    {
        Assert.Null(CopyRules.ValidateSubstitution("from", null, "to", null));
        Assert.Null(CopyRules.ValidateSubstitution("from", "A-", "to", "B-"));
        Assert.Contains("go together", CopyRules.ValidateSubstitution("from", "A-", "to", " "), StringComparison.Ordinal);
        Assert.Contains("go together", CopyRules.ValidateSubstitution("from", null, "to", "B-"), StringComparison.Ordinal);
        Assert.Contains("at most 32", CopyRules.ValidateSubstitution("from", new string('A', 33), "to", "B"), StringComparison.Ordinal);
        Assert.Contains("spaces or '|'", CopyRules.ValidateSubstitution("from", "A B", "to", "C"), StringComparison.Ordinal);
        Assert.Equal("B-01-02", CopyRules.Substitute("A-01-02", "a-", "B-"));
        Assert.Equal("X-01", CopyRules.Substitute("X-01", "A-", "B-"));
        Assert.Equal("A-01", CopyRules.Substitute("A-01", null, "B-"));
        Assert.Equal("A-01", CopyRules.Substitute("A-01", "A-", null));
        Assert.True(CopyRules.StartsWith("a-01", "A-"));
        Assert.False(CopyRules.StartsWith("B-01", "A-"));
        Assert.False(CopyRules.StartsWith(null, "A-"));
        Assert.Throws<ArgumentNullException>(() => CopyRules.Substitute(null!, "A", "B"));
    }



    [Fact]
    public void The_requests_default_to_bringing_everything()
    {
        var site = new SiteCopyRequest("DC2", "Second");
        var zone = new ZoneCopyRequest("A02", "Aisle 2");
        var location = new LocationCopyRequest("A-02-01", "L9");
        var range = new LocationRangeCopyRequest("A-", "B-");

        Assert.Equal((true, true, true, (string?)null, (string?)null, (string?)null), (site.Settings, site.Zones, site.Locations, site.TimeZone, site.CodePrefixFrom, site.CodePrefixTo));
        Assert.Equal((true, true, (long?)null), (zone.Settings, zone.Locations, zone.TargetSiteId));
        Assert.Equal(((string?)null, (long?)null), (location.WalkSequence, location.ZoneId));
        Assert.Equal(((long?)null, (string?)null, (string?)null), (range.ZoneId, range.WalkPrefixFrom, range.WalkPrefixTo));
    }



    [Fact]
    public async Task The_placeholder_answers_unavailable_and_the_exception_carries_its_code()
    {
        var copier = new NoCopies();

        var site = await Assert.ThrowsAsync<CopyException>(() => copier.CopySiteAsync(1, new SiteCopyRequest("DC2", "Second"), "admin", CancellationToken.None));
        var zone = await Assert.ThrowsAsync<CopyException>(() => copier.CopyZoneAsync(1, 1, new ZoneCopyRequest("A02", "Aisle 2"), "admin", CancellationToken.None));
        var location = await Assert.ThrowsAsync<CopyException>(() => copier.CopyLocationAsync(1, 1, new LocationCopyRequest("A-02-01", "L9"), "admin", CancellationToken.None));
        var range = await Assert.ThrowsAsync<CopyException>(() => copier.CopyLocationRangeAsync(1, new LocationRangeCopyRequest("A-", "B-"), "admin", CancellationToken.None));

        Assert.All([site, zone, location, range], e => Assert.Equal(CopyErrorCodes.Unavailable, e.Code));
        await Assert.ThrowsAsync<ArgumentNullException>(() => copier.CopySiteAsync(1, null!, "admin", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => copier.CopyLocationRangeAsync(1, new LocationRangeCopyRequest("A-", "B-"), " ", CancellationToken.None));
        Assert.Equal(CopyErrorCodes.Unavailable, new CopyException().Code);
        Assert.Equal("boom", new CopyException("boom").Message);
        Assert.Equal("cause", new CopyException("boom", new InvalidOperationException("cause")).InnerException!.Message);
        Assert.Equal(CopyErrorCodes.Invalid, new CopyException(CopyErrorCodes.Invalid, "x", new InvalidOperationException()).Code);
        Assert.Throws<ArgumentNullException>(() => new CopyException(null!, "x"));
    }



    [Fact]
    public void The_module_declares_its_codes_and_borrows_the_entities_permissions()
    {
        Assert.Equal("copies", CopiesModule.Descriptor.Name);
        Assert.Empty(CopiesModule.Descriptor.Permissions);
        Assert.Equal(["copies.not_found", "copies.invalid", "copies.code_taken", "copies.barcode_taken", "copies.unavailable"], CopiesModule.Descriptor.ErrorCodes.Select(c => c.Code));
        Assert.Throws<ArgumentNullException>(() => CopiesModule.AddWmsCopiesModule(null!));
    }



    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void Every_copyable_row_carries_an_optional_copied_from_id_that_its_info_reports(string provider)
    {
        using var context = new WmsDbContext(ModelConventionsTests.Options<WmsDbContext>(provider));
        var site = new Site { CopiedFromId = 7 };
        var zone = new Zone { SiteId = 1, CopiedFromId = 8 };
        var location = new Location { SiteId = 1, CopiedFromId = 9 };
        site.Apply(new SiteDraft("DC2", "Second", "UTC"), Now, "admin");
        zone.Apply(new ZoneDraft("A02", "Aisle 2", ZoneType.Pick, null, false, null), Now, "admin");
        location.Apply(new LocationDraft("A-02-01", "L9", 3, "A-0201"), Now, "admin");

        foreach (var type in new[] { typeof(Site), typeof(Zone), typeof(Location) })
        {
            var column = context.Model.FindEntityType(type)!.FindProperty(nameof(Site.CopiedFromId))!;

            Assert.True(column.IsNullable);
            Assert.Equal("copied_from_id", column.GetColumnName());
        }

        Assert.Equal((7L, 8L, 9L), (site.ToInfo().CopiedFromId, zone.ToInfo().CopiedFromId, location.ToInfo().CopiedFromId));
        Assert.Null(new SiteInfo(1, "DC1", "First", "UTC", true, Now, "admin", 0).CopiedFromId);
    }
}
