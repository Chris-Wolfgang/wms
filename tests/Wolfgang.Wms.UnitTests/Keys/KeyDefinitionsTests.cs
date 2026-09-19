// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.UnitTests.Keys;

public sealed class KeyDefinitionsTests
{
    [Fact]
    public void Enumerates_static_fields_and_properties_of_the_key_type_in_declaration_order()
    {
        var permissions = KeyDefinitions.Enumerate<Permission>(typeof(SamplePermissions));

        Assert.Equal(["picking.release", "picking.cancel", "picking.resolve"], permissions.Select(p => p.Name));
    }



    [Fact]
    public void Enumerates_typed_setting_keys_through_their_untyped_base()
    {
        var settings = KeyDefinitions.Enumerate<SettingKey>(typeof(SampleSettings));

        Assert.Equal(["picking.lease_timeout", "picking.max_totes"], settings.Select(s => s.Name));
        Assert.Equal([typeof(TimeSpan), typeof(int)], settings.Select(s => s.ValueType));
    }



    [Fact]
    public void Ignores_members_of_other_types()
    {
        Assert.Empty(KeyDefinitions.Enumerate<JobName>(typeof(SamplePermissions)));
    }



    [Fact]
    public void Duplicate_key_names_in_one_definitions_class_are_an_error()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => KeyDefinitions.Enumerate<IssueType>(typeof(DuplicateIssueTypes)));

        Assert.Contains("picking.short_pick", exception.Message, StringComparison.Ordinal);
    }



    [Fact]
    public void Uninitialised_definitions_are_an_error()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => KeyDefinitions.Enumerate<JobName>(typeof(NullJob)));

        Assert.Contains("NullJob.Missing", exception.Message, StringComparison.Ordinal);
    }



    [Fact]
    public void Every_key_kind_is_named_for_duplicate_detection()
    {
        Assert.Throws<InvalidOperationException>(() => KeyDefinitions.Enumerate<FeatureFlag>(typeof(DuplicateOfEveryKind)));
        Assert.Throws<InvalidOperationException>(() => KeyDefinitions.Enumerate<LicenseLimit>(typeof(DuplicateOfEveryKind)));
        Assert.Throws<InvalidOperationException>(() => KeyDefinitions.Enumerate<LicenseFeature>(typeof(DuplicateOfEveryKind)));
        Assert.Throws<InvalidOperationException>(() => KeyDefinitions.Enumerate<ErrorCode>(typeof(DuplicateOfEveryKind)));
        Assert.Throws<InvalidOperationException>(() => KeyDefinitions.Enumerate<JobName>(typeof(DuplicateOfEveryKind)));
        Assert.Throws<InvalidOperationException>(() => KeyDefinitions.Enumerate<string>(typeof(DuplicateOfEveryKind)));
    }



    [Fact]
    public void Definitions_type_is_required()
    {
        Assert.Throws<ArgumentNullException>(() => KeyDefinitions.Enumerate<Permission>(null!));
    }



    private static class SamplePermissions
    {
        public static readonly Permission Release = new("picking.release", "Release a wave");
        public static readonly Permission Cancel = new("picking.cancel", "Cancel a wave");
        public static Permission Resolve { get; } = new("picking.resolve", "Resolve a tote");
        public static readonly string NotAKey = "ignored";
    }



    private static class SampleSettings
    {
        public static readonly SettingKey<TimeSpan> LeaseTimeout = new("picking.lease_timeout", TimeSpan.FromMinutes(15), "How long a picker holds a task.");
        public static readonly SettingKey<int> MaxTotes = new("picking.max_totes", 1, "Totes a picker may carry.");
    }



    private static class DuplicateIssueTypes
    {
        public static readonly IssueType First = new("picking.short_pick", "A pick came up short");
        public static readonly IssueType Second = new("picking.short_pick", "Same name again");
    }



    private static class NullJob
    {
        public static JobName? Missing => null;
    }



    private static class DuplicateOfEveryKind
    {
        public static readonly FeatureFlag Flag1 = new("dup");
        public static readonly FeatureFlag Flag2 = new("dup");
        public static readonly LicenseLimit Limit1 = new("dup", "d");
        public static readonly LicenseLimit Limit2 = new("dup", "d");
        public static readonly LicenseFeature Feature1 = new("dup", "d");
        public static readonly LicenseFeature Feature2 = new("dup", "d");
        public static readonly ErrorCode Error1 = new("dup", 400, "m", "a", ErrorSeverity.Error);
        public static readonly ErrorCode Error2 = new("dup", 400, "m", "a", ErrorSeverity.Error);
        public static readonly JobName Job1 = new("dup", "d");
        public static readonly JobName Job2 = new("dup", "d");
        public static readonly string Text1 = "dup";
        public static readonly string Text2 = "dup";
    }
}
