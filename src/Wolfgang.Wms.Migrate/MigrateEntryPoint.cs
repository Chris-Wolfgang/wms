// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Wms.Migrate;

/// <summary>
/// Entry point of <c>wms-migrate</c>. An explicit class rather than top-level statements so the type is not
/// named <c>Program</c> like the API host's, which shares a test assembly with this tool. Excluded from
/// coverage like the implicit entry point would be: it only wires Ctrl+C and the console streams to
/// <see cref="MigrateProgram.RunAsync(IReadOnlyList{string}, TextReader, TextWriter, TextWriter, Microsoft.Extensions.Configuration.IConfiguration, CancellationToken)"/>, which the tests drive directly.
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
        using var ctrlC = new CancelKeyPressSubscription(cancellation);   // disposed first: the static event never keeps a handler over a disposed source

        return await MigrateProgram.RunAsync(args, Console.In, Console.Out, Console.Error, configuration: null, cancellation.Token).ConfigureAwait(false);
    }



    /// <summary>
    /// Cancels the token source it is given on Ctrl+C for as long as it lives; disposing it unsubscribes from
    /// <see cref="Console.CancelKeyPress"/>, which is static and would otherwise outlive the source.
    /// </summary>
    private sealed class CancelKeyPressSubscription : IDisposable
    {
        private readonly ConsoleCancelEventHandler _handler;

        private readonly Lock _gate = new();

        private bool _disposed;

        public CancelKeyPressSubscription(CancellationTokenSource source)
        {
            // The gate closes the race between a Ctrl+C already being handled and the run finishing: a handler
            // holding it completes Cancel before Dispose can proceed, and one arriving after Dispose finds the
            // flag set and never touches the source, which the caller disposes next.
            _handler = (_, e) =>
            {
                e.Cancel = true;
                lock (_gate)
                {
                    if (!_disposed)
                    {
                        source.Cancel();
                    }
                }
            };
            Console.CancelKeyPress += _handler;
        }

        public void Dispose()
        {
            Console.CancelKeyPress -= _handler;
            lock (_gate)
            {
                _disposed = true;
            }
        }
    }
}
