// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Wms.Admin;

/// <summary>
/// Entry point of <c>wms-admin</c>. An explicit class rather than top-level statements so the type is not
/// named <c>Program</c> like the API host's, which shares a test assembly with this tool. Excluded from
/// coverage like the implicit entry point would be: it only wires Ctrl+C and the console streams to
/// <see cref="AdminProgram.RunAsync"/>, which the tests drive directly.
/// </summary>
[ExcludeFromCodeCoverage]
public static class AdminEntryPoint
{
    /// <summary>
    /// Runs the tool with Ctrl+C cancelling the exchange.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        return await AdminProgram.RunAsync(args, Console.Out, Console.Error, configuration: null, TimeProvider.System, cancellation.Token).ConfigureAwait(false);
    }
}
