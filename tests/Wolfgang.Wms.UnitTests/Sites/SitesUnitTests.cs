// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Database.Settings;
using Wolfgang.Wms.Infrastructure.Sites;
using Wolfgang.Wms.UnitTests.Database;

namespace Wolfgang.Wms.UnitTests.Sites;

public sealed class SitesUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly SiteDraft Valid = new("HAM-01", "Hamburg", "Europe/Berlin");



    [Fact]
    public void A_draft_with_a_code_a_name_and_a_known_time_zone_is_valid()
    {
        Assert.Null(SiteRules.Validate(Valid));
        Assert.Null(SiteRules.Validate(new SiteDraft(" dc_1 ", "Distribution centre", "UTC", IsActive: false)));
        Assert.Equal("HAM-01", SiteRules.Normalize(" ham-01 "));
        Assert.Throws<ArgumentNullException>(() => SiteRules.Validate(null!));
        Assert.Throws<ArgumentNullException>(() => SiteRules.Normalize(null!));
    }



    public static TheoryData<SiteDraft, string> InvalidDrafts => new()
    {
        { Valid with { Code = " " }, "code is required" },
        { Valid with { Code = new string('x', 33) }, "code must be at most 32" },
        { Valid with { Code = "HAM 01" }, "code may contain letters, digits, '-' and '_' only" },
        { Valid with { Code = "HAM/01" }, "code may contain letters, digits, '-' and '_' only" },
        { Valid with { Name = "" }, "name is required" },
        { Valid with { Name = new string('x', 129) }, "name must be at most 128" },
        { Valid with { TimeZone = " " }, "timeZone is required" },
        { Valid with { TimeZone = null }, "timeZone is required" },
        { Valid with { TimeZone = "Mars/Olympus" }, "not a known time zone id" },
    };



    [Theory]
    [MemberData(nameof(InvalidDrafts))]
    public void An_invalid_draft_names_the_first_problem(SiteDraft draft, string expectedFragment)
    {
        var reason = SiteRules.Validate(draft);

        Assert.NotNull(reason);
        Assert.Contains(expectedFragment, reason, StringComparison.Ordinal);
    }



    [Fact]
    public void The_row_round_trips_a_draft_and_normalizes_the_code()
    {
        var row = new Site();

        row.Apply(new SiteDraft(" ham-01 ", "  Hamburg  ", " Europe/Berlin "), Now, "admin");
        var info = row.ToInfo();

        Assert.Equal(("ham-01", "HAM-01", "Hamburg", "Europe/Berlin", true), (row.Code, row.CodeNormalized, row.Name, row.TimeZone, row.IsActive));
        Assert.Equal(new SiteInfo(0, "ham-01", "Hamburg", "Europe/Berlin", true, Now, "admin", 0), info);
        Assert.Equal("\"0\"", info.Etag);
        Assert.Throws<ArgumentNullException>(() => row.Apply(null!, Now, "admin"));
    }



    [Fact]
    public async Task The_placeholders_answer_unavailable_and_no_open_releases()
    {
        var store = new NoSites();
        var releases = new NoOpenReleases();

        var list = await Assert.ThrowsAsync<SiteException>(() => store.ListAsync(SiteScope.Everywhere, CancellationToken.None));
        var find = await Assert.ThrowsAsync<SiteException>(() => store.FindAsync(1, CancellationToken.None));
        var create = await Assert.ThrowsAsync<SiteException>(() => store.CreateAsync(Valid, "admin", CancellationToken.None));
        var update = await Assert.ThrowsAsync<SiteException>(() => store.UpdateAsync(1, Valid, "admin", CancellationToken.None));

        Assert.All([list, find, create, update], e => Assert.Equal(SiteErrorCodes.Unavailable, e.Code));
        Assert.Equal(0, await releases.CountOpenAsync(1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.ListAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.CreateAsync(null!, "admin", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdateAsync(1, Valid, " ", CancellationToken.None));
        Assert.Equal(SiteErrorCodes.Unavailable, new SiteException().Code);
        Assert.Equal("boom", new SiteException("boom").Message);
        Assert.Equal("cause", new SiteException("boom", new InvalidOperationException("cause")).InnerException!.Message);
        Assert.Equal(SiteErrorCodes.Invalid, new SiteException(SiteErrorCodes.Invalid, "x", new InvalidOperationException()).Code);
        Assert.Throws<ArgumentNullException>(() => new SiteException(null!, "x"));
    }



    [Fact]
    public void The_module_declares_its_permissions_and_codes()
    {
        Assert.Equal("sites", SitesModule.Descriptor.Name);
        Assert.Equal(["sites.read", "sites.write"], SitesModule.Descriptor.Permissions.Select(p => p.Name));
        Assert.Equal([BuiltInRole.Supervisor, BuiltInRole.Support, BuiltInRole.Viewer], SitesModule.Read.DefaultRoles);
        Assert.Empty(SitesModule.Write.DefaultRoles);
        Assert.Equal(["sites.not_found", "sites.code_taken", "sites.invalid", "sites.has_open_releases", "sites.unavailable"], SitesModule.Descriptor.ErrorCodes.Select(c => c.Code));
        Assert.Throws<ArgumentNullException>(() => SitesModule.AddWmsSitesModule(null!));
    }



    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    public async Task Core_site_is_a_versioned_table_with_a_unique_normalized_code(string provider)
    {
        using var context = new WmsDbContext(ModelConventionsTests.Options<WmsDbContext>(provider));
        var entity = context.Model.FindEntityType(typeof(Site))!;
        var hierarchy = new EfSettingScopeHierarchy(context);

        Assert.Equal("site", entity.GetTableName());
        Assert.Equal("layout", entity.GetSchema());
        Assert.Equal(SiteRules.CodeLength, entity.FindProperty(nameof(Site.CodeNormalized))!.GetMaxLength());
        Assert.False(entity.FindProperty(nameof(Site.TimeZone))!.IsNullable);
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique && string.Equals(i.GetDatabaseName(), "ux_site_code_normalized", StringComparison.Ordinal));
        Assert.Contains(entity.GetIndexes(), i => string.Equals(i.GetDatabaseName(), "ix_site_row_version", StringComparison.Ordinal));
        Assert.Equal(["trg_site_row_version"], entity.GetDeclaredTriggers().Select(t => t.ModelName));
        Assert.Equal(SettingScopeRef.Organization, await hierarchy.ParentAsync(SettingScopeRef.Site(7), CancellationToken.None));
        Assert.Null(await hierarchy.ParentAsync(SettingScopeRef.Organization, CancellationToken.None));
        Assert.Null(await hierarchy.ParentAsync(SettingScopeRef.Sku(3), CancellationToken.None));
        Assert.Empty(await hierarchy.ChildrenAsync(SettingScopeRef.Sku(7), CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => new SiteConfiguration().Configure(null!));
        Assert.Throws<ArgumentNullException>(() => new EfSettingScopeHierarchy(null!));
    }
}
