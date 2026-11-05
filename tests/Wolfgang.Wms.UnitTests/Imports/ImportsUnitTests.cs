// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Reflection;
using Wolfgang.Wms.Core.Docs;
using Wolfgang.Wms.Core.Imports;
using Wolfgang.Wms.Core.Zones;

namespace Wolfgang.Wms.UnitTests.Imports;

public sealed class ImportsUnitTests
{
    [Theory]
    [InlineData(null, ImportPolicy.AcceptValidRows)]
    [InlineData(" ", ImportPolicy.AcceptValidRows)]
    [InlineData("all_or_nothing", ImportPolicy.AllOrNothing)]
    [InlineData("All-Or-Nothing", ImportPolicy.AllOrNothing)]
    [InlineData("AllOrNothing", ImportPolicy.AllOrNothing)]
    [InlineData("accept_valid_rows", ImportPolicy.AcceptValidRows)]
    [InlineData("validate_only", ImportPolicy.ValidateOnly)]
    [InlineData("validateOnly", ImportPolicy.ValidateOnly)]
    public void A_policy_is_parsed_from_its_query_value_with_the_fallback_for_none(string? text, ImportPolicy expected)
    {
        Assert.Equal(expected, ImportRules.ParsePolicy(text, ImportPolicy.AcceptValidRows));
    }



    [Fact]
    public void An_unknown_policy_a_size_outside_the_limits_and_the_names_round_trip()
    {
        Assert.Null(ImportRules.ParsePolicy("yolo", ImportPolicy.AllOrNothing));
        Assert.Equal("The file has no rows.", ImportRules.ValidateSize(0));
        Assert.Null(ImportRules.ValidateSize(ImportRules.MaxRows));
        Assert.Contains("at most 10000", ImportRules.ValidateSize(ImportRules.MaxRows + 1), StringComparison.Ordinal);
        Assert.Equal(["all_or_nothing", "accept_valid_rows", "validate_only"], Enum.GetValues<ImportPolicy>().Select(ImportRules.Name));
        Assert.All(Enum.GetValues<ImportPolicy>(), p => Assert.Equal(p, ImportRules.ParsePolicy(ImportRules.Name(p), ImportPolicy.ValidateOnly)));
    }



    [Fact]
    public void A_run_writes_under_the_policy_only_when_its_rows_allow()
    {
        var clean = Rows(ImportRowOutcome.Inserted, ImportRowOutcome.Unchanged);
        var withFailure = Rows(ImportRowOutcome.Inserted, ImportRowOutcome.Failed);
        var nothingToDo = Rows(ImportRowOutcome.Unchanged, ImportRowOutcome.Failed);

        Assert.True(ImportRules.ShouldWrite(ImportPolicy.AllOrNothing, clean));
        Assert.False(ImportRules.ShouldWrite(ImportPolicy.AllOrNothing, withFailure));
        Assert.False(ImportRules.ShouldWrite(ImportPolicy.AllOrNothing, []));
        Assert.True(ImportRules.ShouldWrite(ImportPolicy.AcceptValidRows, withFailure));
        Assert.False(ImportRules.ShouldWrite(ImportPolicy.AcceptValidRows, nothingToDo));
        Assert.False(ImportRules.ShouldWrite(ImportPolicy.ValidateOnly, clean));
        Assert.Throws<ArgumentNullException>(() => ImportRules.ShouldWrite(ImportPolicy.AllOrNothing, null!));
    }



    [Fact]
    public void The_result_counts_its_rows_and_renders_them_as_csv()
    {
        var rows = new List<ImportRowResult>
        {
            new(1, "A01", ImportRowOutcome.Inserted, null, null),
            new(2, "A02", ImportRowOutcome.Updated, null, null),
            new(3, "A03", ImportRowOutcome.Deleted, null, null),
            new(4, "A04", ImportRowOutcome.Unchanged, null, null),
            new(5, "A,05", ImportRowOutcome.Failed, "zones.invalid", "name is \"required\", really"),
        };

        var result = ImportResult.From("zones", ImportPolicy.AcceptValidRows, written: true, rows);

        Assert.Equal((1, 1, 1, 1, 1, true), (result.Inserted, result.Updated, result.Deleted, result.Unchanged, result.Failed, result.Written));
        Assert.Equal("row,key,outcome,code,message\r\n1,A01,Inserted,,\r\n2,A02,Updated,,\r\n3,A03,Deleted,,\r\n4,A04,Unchanged,,\r\n5,\"A,05\",Failed,zones.invalid,\"name is \"\"required\"\", really\"\r\n", result.ToCsv());
        Assert.Throws<ArgumentNullException>(() => ImportResult.From("zones", ImportPolicy.AllOrNothing, written: false, null!));
        Assert.Throws<ArgumentException>(() => ImportResult.From(" ", ImportPolicy.AllOrNothing, written: false, rows));
        Assert.Throws<ArgumentNullException>(() => new ImportResult("zones", ImportPolicy.AllOrNothing, false, 0, 0, 0, 0, 0, null!));
    }



    [Fact]
    public void The_rows_turn_into_the_drafts_the_stores_take()
    {
        var zone = new ZoneImportRow("A01", "Aisle 1", ZoneType.Bulk, ImportAction.Delete, "A", IsRejectLane: false, IsActive: false).ToDraft();
        var location = new LocationImportRow("A-01-01", "L1", "A01", "A-0101", ImportAction.Upsert, IsPickable: false).ToDraft(42);

        Assert.Equal(new ZoneDraft("A01", "Aisle 1", ZoneType.Bulk, "A", false, null, false), zone);
        Assert.Equal((42L, "A-01-01", "L1", "A-0101", false, true), (location.ZoneId, location.Code, location.Barcode, location.WalkSequence, location.IsPickable, location.IsActive));
        var minimal = new ZoneImportRow("B", "b", default, default);

        Assert.Equal((ZoneType.Pick, (string?)null, false, true, ImportAction.Upsert), (minimal.Type, minimal.WalkOrderPrefix, minimal.IsRejectLane, minimal.IsActive, minimal.Action));   // an absent JSON property lands on the enum's first value
    }



    [Fact]
    public async Task The_placeholder_answers_unavailable_and_the_exception_carries_its_code()
    {
        var importer = new NoImports();

        var zones = await Assert.ThrowsAsync<ImportException>(() => importer.ImportZonesAsync(1, [], ImportPolicy.AllOrNothing, "admin", CancellationToken.None));
        var locations = await Assert.ThrowsAsync<ImportException>(() => importer.ImportLocationsAsync(1, [], ImportPolicy.AllOrNothing, "admin", CancellationToken.None));

        Assert.All([zones, locations], e => Assert.Equal(ImportErrorCodes.Unavailable, e.Code));
        await Assert.ThrowsAsync<ArgumentNullException>(() => importer.ImportZonesAsync(1, null!, ImportPolicy.AllOrNothing, "admin", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => importer.ImportLocationsAsync(1, [], ImportPolicy.AllOrNothing, " ", CancellationToken.None));
        Assert.Equal(ImportErrorCodes.Unavailable, new ImportException().Code);
        Assert.Equal("boom", new ImportException("boom").Message);
        Assert.Equal("cause", new ImportException("boom", new InvalidOperationException("cause")).InnerException!.Message);
        Assert.Equal(ImportErrorCodes.Invalid, new ImportException(ImportErrorCodes.Invalid, "x", new InvalidOperationException()).Code);
        Assert.Throws<ArgumentNullException>(() => new ImportException(null!, "x"));
    }



    [Fact]
    public void The_module_declares_its_permission_codes_and_defaults()
    {
        Assert.Equal("imports", ImportsModule.Descriptor.Name);
        Assert.Equal(["imports.write"], ImportsModule.Descriptor.Permissions.Select(p => p.Name));
        Assert.Empty(ImportsModule.Write.DefaultRoles);
        Assert.Equal(["imports.site_not_found", "imports.invalid", "imports.duplicate_in_file", "imports.reference_not_found", "imports.key_not_found", "imports.resolution_zone", "imports.unavailable"], ImportsModule.Descriptor.ErrorCodes.Select(c => c.Code));
        Assert.Equal((ImportPolicy.AllOrNothing, ImportPolicy.AcceptValidRows), (ImportsModule.ZonesDefaultPolicy, ImportsModule.LocationsDefaultPolicy));
        Assert.Throws<ArgumentNullException>(() => ImportsModule.AddWmsImportsModule(null!));
    }



    [Fact]
    public void Every_format_documents_exactly_the_fields_of_its_row_type_and_renders()
    {
        foreach (var format in ImportFormats.All)
        {
            var parameters = format.RowType.GetConstructors().Single().GetParameters();

            Assert.Equal(parameters.Select(p => char.ToLowerInvariant(p.Name![0]) + p.Name[1..]), format.Fields.Select(f => f.Name));
            Assert.Equal(parameters.Select(p => !p.HasDefaultValue && !p.ParameterType.IsEnum), format.Fields.Select(f => f.Required));   // an absent enum lands on its first value, so it is optional
            Assert.Contains(format.NaturalKey, format.Fields.Select(f => f.Name));
        }

        var page = ReferencePages.ImportFormats(ImportFormats.All);

        Assert.Equal(["zones", "locations"], ImportFormats.All.Select(f => f.Entity));
        Assert.Contains("## `zones`", page, StringComparison.Ordinal);
        Assert.Contains("| Default policy | `all_or_nothing` |", page, StringComparison.Ordinal);
        Assert.Contains("| `action` | Upsert \\| Delete | no | `Upsert` |", page, StringComparison.Ordinal);
        Assert.Contains("| `zoneCode` | string | yes |  |", page, StringComparison.Ordinal);
    }



    private static List<ImportRowResult> Rows(params ImportRowOutcome[] outcomes)
    {
        return outcomes.Select((o, i) => new ImportRowResult(i + 1, "K" + i, o, o == ImportRowOutcome.Failed ? "x" : null, null)).ToList();
    }
}
