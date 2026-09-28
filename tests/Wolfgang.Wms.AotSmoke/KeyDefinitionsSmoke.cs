// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.AotSmoke;

/// <summary>
/// <see cref="KeyDefinitions.Enumerate{TKey}"/> reads static fields and properties by reflection; under NativeAOT
/// the trimmer must keep them and nothing may depend on metadata the runtime lacks (MetadataToken threw here).
/// </summary>
internal static class KeyDefinitionsSmoke
{
    public static void FieldsAndPropertiesSortedByName()
    {
        var permissions = KeyDefinitions.Enumerate<Permission>(typeof(MixedPermissions));

        Smoke.SequenceEqual(["smoke.alpha", "smoke.mid", "smoke.release", "smoke.resolve", "smoke.zeta"], permissions.Select(p => p.Name));
    }



    public static void EveryKeyKind()
    {
        Smoke.SequenceEqual(["smoke.flag"], KeyDefinitions.Enumerate<FeatureFlag>(typeof(EveryKind)).Select(k => k.Name));
        Smoke.SequenceEqual(["smoke.permission"], KeyDefinitions.Enumerate<Permission>(typeof(EveryKind)).Select(k => k.Name));
        Smoke.SequenceEqual(["smoke.limit"], KeyDefinitions.Enumerate<LicenseLimit>(typeof(EveryKind)).Select(k => k.Name));
        Smoke.SequenceEqual(["smoke.feature"], KeyDefinitions.Enumerate<LicenseFeature>(typeof(EveryKind)).Select(k => k.Name));
        Smoke.SequenceEqual(["smoke.error"], KeyDefinitions.Enumerate<ErrorCode>(typeof(EveryKind)).Select(k => k.Code));
        Smoke.SequenceEqual(["smoke.issue"], KeyDefinitions.Enumerate<IssueType>(typeof(EveryKind)).Select(k => k.Name));
        Smoke.SequenceEqual(["smoke.job"], KeyDefinitions.Enumerate<JobName>(typeof(EveryKind)).Select(k => k.Name));
        Smoke.SequenceEqual(["smoke.setting"], KeyDefinitions.Enumerate<SettingKey>(typeof(EveryKind)).Select(k => k.Name));
    }



    public static void TypedSettingKeysThroughTheirBase()
    {
        var settings = KeyDefinitions.Enumerate<SettingKey>(typeof(Settings));

        Smoke.SequenceEqual(["smoke.lease_timeout", "smoke.max_totes"], settings.Select(s => s.Name));
        Smoke.SequenceEqual([typeof(TimeSpan).FullName!, typeof(int).FullName!], settings.Select(s => s.ValueType.FullName!));
    }



    public static void MembersOfOtherTypesIgnored()
    {
        Smoke.SequenceEqual([], KeyDefinitions.Enumerate<JobName>(typeof(MixedPermissions)).Select(k => k.Name));
    }



    public static void DuplicateNamesRejected()
    {
        Smoke.Throws<InvalidOperationException>(() => KeyDefinitions.Enumerate<IssueType>(typeof(DuplicateIssues)), "smoke.short_pick");
    }



    public static void UninitialisedDefinitionsRejected()
    {
        Smoke.Throws<InvalidOperationException>(() => KeyDefinitions.Enumerate<JobName>(typeof(NullJob)), "NullJob.Missing");
    }



    // Declaration order deliberately differs from name order and interleaves fields with properties.
    private static class MixedPermissions
    {
        public static readonly Permission Release = new("smoke.release", "Release");
        public static Permission Resolve { get; } = new("smoke.resolve", "Resolve");
        public static readonly Permission Zeta = new("smoke.zeta", "Zeta");
        public static Permission Alpha { get; } = new("smoke.alpha", "Alpha");
        public static readonly Permission Mid = new("smoke.mid", "Mid");
        public static readonly string NotAKey = "ignored";
    }



    private static class EveryKind
    {
        public static readonly FeatureFlag Flag = new("smoke.flag");
        public static readonly Permission Permission = new("smoke.permission", "Permission");
        public static LicenseLimit Limit { get; } = new("smoke.limit", "Limit");
        public static readonly LicenseFeature Feature = new("smoke.feature", "Feature");
        public static ErrorCode Error { get; } = new("smoke.error", 400, "Message", "anchor", ErrorSeverity.Error);
        public static readonly IssueType Issue = new("smoke.issue", "Issue");
        public static JobName Job { get; } = new("smoke.job", "Job");
        public static readonly SettingKey<int> Setting = new("smoke.setting", 1, "Setting");
    }



    private static class Settings
    {
        public static SettingKey<int> MaxTotes { get; } = new("smoke.max_totes", 1, "Totes");
        public static readonly SettingKey<TimeSpan> LeaseTimeout = new("smoke.lease_timeout", TimeSpan.FromMinutes(15), "Lease");
    }



    private static class DuplicateIssues
    {
        public static readonly IssueType First = new("smoke.short_pick", "First");
        public static IssueType Second { get; } = new("smoke.short_pick", "Second");
    }



    private static class NullJob
    {
        public static JobName? Missing => null;
    }
}
