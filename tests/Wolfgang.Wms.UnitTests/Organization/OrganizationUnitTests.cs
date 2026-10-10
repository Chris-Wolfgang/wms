// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Organization;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Organization;
using Wolfgang.Wms.UnitTests.Database;

namespace Wolfgang.Wms.UnitTests.Organization;

/// <summary>
/// E16.0 without a database: the draft rules (required fields, lengths, a known time zone and culture, an
/// e-mail that looks like one, a logo that is an image data URL), the anonymous view, the row's round trip,
/// the placeholder store, the exception, the module's permissions and codes, and the table's shape.
/// </summary>
public sealed class OrganizationUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly OrganizationDraft Valid = new
    (
        "Acme Logistics",
        "Acme Logistics GmbH",
        "data:image/png;base64,iVBORw0KGgo=",
        "Europe/Berlin",
        "de-DE",
        new OrganizationAddress("Industriestraße 1", null, "Hamburg", null, "20457", "Germany"),
        new OrganizationContact("Operations", "ops@acme.example", "+49 40 123456"),
        new OrganizationContact("Service desk", "help@acme.example", null)
    );



    [Fact]
    public void A_complete_draft_is_valid_and_a_minimal_one_too()
    {
        Assert.Null(OrganizationRules.Validate(Valid));
        Assert.Null(OrganizationRules.Validate(new OrganizationDraft("Acme", null, null, "UTC", "en-US", null, null, null)));
        Assert.Throws<ArgumentNullException>(() => OrganizationRules.Validate(null!));
    }



    public static TheoryData<OrganizationDraft, string> InvalidDrafts => new()
    {
        { Valid with { Name = " " }, "name is required" },
        { Valid with { Name = new string('x', 129) }, "name must be at most 128" },
        { Valid with { LegalName = new string('x', 257) }, "legalName must be at most 256" },
        { Valid with { LogoDataUrl = "https://acme.example/logo.png" }, "logoDataUrl must be a data:image" },
        { Valid with { LogoDataUrl = "data:image/png;base64," + new string('A', 262_200) }, "logoDataUrl must be at most" },
        { Valid with { TimeZone = "" }, "timeZone is required" },
        { Valid with { TimeZone = "Mars/Olympus" }, "not a known time zone id" },
        { Valid with { Locale = "xx-NOPE" }, "not a culture name" },
        { Valid with { Address = Valid.Address! with { City = "" } }, "address.city is required" },
        { Valid with { Address = Valid.Address! with { PostalCode = new string('9', 33) } }, "address.postalCode must be at most 32" },
        { Valid with { PrimaryContact = Valid.PrimaryContact! with { Email = "not-an-address" } }, "primaryContact.email must be an e-mail address" },
        { Valid with { SupportContact = Valid.SupportContact! with { Name = "" } }, "supportContact.name is required" },
    };



    [Theory]
    [MemberData(nameof(InvalidDrafts))]
    public void An_invalid_draft_names_the_first_problem(OrganizationDraft draft, string expectedFragment)
    {
        var reason = OrganizationRules.Validate(draft);

        Assert.NotNull(reason);
        Assert.Contains(expectedFragment, reason, StringComparison.Ordinal);
    }



    [Fact]
    public void The_row_round_trips_a_draft_and_reports_the_anonymous_view()
    {
        var row = new Infrastructure.Organization.Organization();

        row.Apply(Valid with { Name = "  Acme Logistics  ", LegalName = " " }, Now, "admin");
        var info = row.ToInfo();
        var minimal = new Infrastructure.Organization.Organization();
        minimal.Apply(new OrganizationDraft("Acme", null, null, "UTC", "en-US", null, null, null), Now, "admin");

        Assert.Equal("Acme Logistics", info.Name);
        Assert.Null(info.LegalName);
        Assert.Equal(Valid.Address, info.Address);
        Assert.Equal(Valid.PrimaryContact, info.PrimaryContact);
        Assert.Equal(Valid.SupportContact, info.SupportContact);
        Assert.Equal((Now, "admin"), (info.UpdatedAt, info.UpdatedBy));
        Assert.Equal(new OrganizationPublicInfo("Acme Logistics", Valid.LogoDataUrl), info.ToPublic());
        Assert.Equal("\"0\"", info.Etag);
        Assert.Null(minimal.ToInfo().Address);
        Assert.Null(minimal.ToInfo().PrimaryContact);
        Assert.Null(minimal.ToInfo().SupportContact);
        Assert.Throws<ArgumentNullException>(() => row.Apply(null!, Now, "admin"));
    }



    [Fact]
    public async Task The_placeholder_answers_unavailable_and_the_exception_carries_its_code()
    {
        var store = new NoOrganization();

        var get = await Assert.ThrowsAsync<OrganizationException>(() => store.GetAsync(CancellationToken.None));
        var create = await Assert.ThrowsAsync<OrganizationException>(() => store.CreateAsync(Valid, "admin", CancellationToken.None));
        var update = await Assert.ThrowsAsync<OrganizationException>(() => store.UpdateAsync(Valid, "admin", CancellationToken.None));

        Assert.All([get, create, update], e => Assert.Equal(OrganizationErrorCodes.Unavailable, e.Code));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.CreateAsync(null!, "admin", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => store.UpdateAsync(Valid, " ", CancellationToken.None));
        Assert.Equal(OrganizationErrorCodes.Unavailable, new OrganizationException().Code);
        Assert.Equal("boom", new OrganizationException("boom").Message);
        Assert.Equal("cause", new OrganizationException("boom", new InvalidOperationException("cause")).InnerException!.Message);
        Assert.Equal(OrganizationErrorCodes.Invalid, new OrganizationException(OrganizationErrorCodes.Invalid, "x", new InvalidOperationException()).Code);
        Assert.Throws<ArgumentNullException>(() => new OrganizationException(null!, "x"));
    }



    [Fact]
    public void The_module_declares_its_permissions_and_codes()
    {
        Assert.Equal("organization", OrganizationModule.Descriptor.Name);
        Assert.Equal(["organization.read", "organization.write"], OrganizationModule.Descriptor.Permissions.Select(p => p.Name));
        Assert.Equal([BuiltInRole.Supervisor, BuiltInRole.Support, BuiltInRole.Viewer], OrganizationModule.Read.DefaultRoles);
        Assert.Empty(OrganizationModule.Write.DefaultRoles);
        Assert.Equal(["organization.not_created", "organization.already_exists", "organization.invalid", "organization.unavailable"], OrganizationModule.Descriptor.ErrorCodes.Select(c => c.Code));
        Assert.Throws<ArgumentNullException>(() => OrganizationModule.AddWmsOrganizationModule(null!));
    }



    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void Core_organization_is_a_versioned_singleton_table(string provider)
    {
        using var context = new WmsDbContext(ModelConventionsTests.Options<WmsDbContext>(provider));
        var entity = context.Model.FindEntityType(typeof(Infrastructure.Organization.Organization))!;

        Assert.Equal("organization", entity.GetTableName());
        Assert.Equal("core", entity.GetSchema());
        Assert.Equal(OrganizationRules.NameLength, entity.FindProperty(nameof(Infrastructure.Organization.Organization.Name))!.GetMaxLength());
        Assert.Equal(OrganizationRules.LogoLength, entity.FindProperty(nameof(Infrastructure.Organization.Organization.LogoDataUrl))!.GetMaxLength());
        Assert.False(entity.FindProperty(nameof(Infrastructure.Organization.Organization.TimeZone))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(Infrastructure.Organization.Organization.AddressLine1))!.IsNullable);
        Assert.Equal(["ix_organization_row_version"], entity.GetIndexes().Select(i => i.GetDatabaseName()));
        Assert.Equal(["trg_organization_row_version"], entity.GetDeclaredTriggers().Select(t => t.ModelName));
        Assert.Throws<ArgumentNullException>(() => new OrganizationConfiguration().Configure(null!));
    }
}
