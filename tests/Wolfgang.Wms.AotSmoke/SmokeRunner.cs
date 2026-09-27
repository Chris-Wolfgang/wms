// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.AotSmoke;

/// <summary>
/// Runs every smoke check, reports each as PASS or FAIL, and returns the process exit code.
/// </summary>
internal static class SmokeRunner
{
    /// <summary>
    /// Every check, by name. Add a line here when a new AOT-sensitive code path appears in an AOT-compatible project,
    /// and list its source file in <c>covered-sources.txt</c>.
    /// </summary>
    private static readonly (string Name, Action Check)[] Checks =
    [
        ("KeyDefinitions: fields and properties sorted by name", KeyDefinitionsSmoke.FieldsAndPropertiesSortedByName),
        ("KeyDefinitions: every key kind", KeyDefinitionsSmoke.EveryKeyKind),
        ("KeyDefinitions: typed setting keys through their base", KeyDefinitionsSmoke.TypedSettingKeysThroughTheirBase),
        ("KeyDefinitions: members of other types ignored", KeyDefinitionsSmoke.MembersOfOtherTypesIgnored),
        ("KeyDefinitions: duplicate names rejected", KeyDefinitionsSmoke.DuplicateNamesRejected),
        ("KeyDefinitions: uninitialised definitions rejected", KeyDefinitionsSmoke.UninitialisedDefinitionsRejected),
    ];



    /// <summary>
    /// Runs the checks, writes one line per check and a summary to <paramref name="output"/>, and returns 0 when all
    /// pass, 1 otherwise.
    /// </summary>
    public static int Run(TextWriter output)
    {
        var failures = 0;
        foreach (var (name, check) in Checks)
        {
            try
            {
                check();
                output.WriteLine($"PASS {name}");
            }
#pragma warning disable CA1031 // A smoke runner reports every failure, whatever its type, instead of stopping at the first.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                failures++;
                output.WriteLine($"FAIL {name}: {exception.GetType().FullName}: {exception.Message}");
            }
        }

        output.WriteLine($"{Checks.Length - failures} passed, {failures} failed");
        return failures == 0 ? 0 : 1;
    }
}
