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
    private static readonly (string Name, Func<Task> Check)[] Checks =
    [
        ("KeyDefinitions: fields and properties sorted by name", Sync(KeyDefinitionsSmoke.FieldsAndPropertiesSortedByName)),
        ("KeyDefinitions: every key kind", Sync(KeyDefinitionsSmoke.EveryKeyKind)),
        ("KeyDefinitions: typed setting keys through their base", Sync(KeyDefinitionsSmoke.TypedSettingKeysThroughTheirBase)),
        ("KeyDefinitions: members of other types ignored", Sync(KeyDefinitionsSmoke.MembersOfOtherTypesIgnored)),
        ("KeyDefinitions: duplicate names rejected", Sync(KeyDefinitionsSmoke.DuplicateNamesRejected)),
        ("KeyDefinitions: uninitialised definitions rejected", Sync(KeyDefinitionsSmoke.UninitialisedDefinitionsRejected)),
        ("SettingCodecs: every built-in type round-trips", Sync(SettingCodecsSmoke.EveryBuiltInTypeRoundTrips)),
        ("SettingCodecs: enum through the built-in codec", Sync(SettingCodecsSmoke.EnumThroughTheBuiltInCodec)),
        ("SettingCodecs: enum through the typed codec", Sync(SettingCodecsSmoke.EnumThroughTheTypedCodec)),
        ("SettingCodecs: JSON codec and unsupported type", Sync(SettingCodecsSmoke.JsonCodecAndUnsupportedType)),
        ("AuthProviderState: challenge scheme follows the setting", AuthProvidersSmoke.ChallengeSchemeFollowsTheSetting),
        ("Authentication: handler activated from HandlerType", AuthProvidersSmoke.HandlerIsActivatedFromItsType),
        ("Licensing: signed key round-trips through JSON and ECDSA", Sync(LicensingSmoke.SignedKeyRoundTrips)),
        ("Licensing: altered or foreign keys refused", Sync(LicensingSmoke.AlteredKeyIsRefused)),
        ("Licensing: embedded vendor key loads", Sync(LicensingSmoke.VendorKeyLoads)),
    ];



    /// <summary>
    /// Runs the checks, writes one line per check and a summary to <paramref name="output"/>, and returns 0 when all
    /// pass, 1 otherwise.
    /// </summary>
    public static async Task<int> RunAsync(TextWriter output)
    {
        var failures = 0;
        foreach (var (name, check) in Checks)
        {
            try
            {
                await check().ConfigureAwait(false);
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



    private static Func<Task> Sync(Action check)
    {
        return () =>
        {
            check();
            return Task.CompletedTask;
        };
    }
}
