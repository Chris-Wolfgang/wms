// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Wms.Migrate;

/// <summary>
/// Entry point of <c>wms-migrate</c>. An explicit class rather than top-level statements so the type is not
/// named <c>Program</c> like the API host's, which shares a test assembly with this tool. Excluded from
/// coverage like the implicit entry point would be: it only wires Ctrl+C and the console streams to
/// <see cref="MigrateProgram.RunAsync"/>, which the tests drive directly.
/// </summary>
[ExcludeFromCodeCoverage]
public static class MigrateEntryPoint
{
    /// <summary>
    /// Runs the tool with Ctrl+C cancelling a running migration.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        return await MigrateProgram.RunAsync(args, Console.Out, Console.Error, configuration: null, cancellation.Token).ConfigureAwait(false);
    }
}
