// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Migrate;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// Applies the schema to a fresh test database the way an operator does: <c>wms-migrate</c> (in process, the
/// executable's own entry point) with the test login's rights, before the host starts. The API never migrates
/// (E4.4: its service account has no schema rights), so every integration test that needs a schema runs this
/// first; the test database itself is created by <see cref="SqlServerTestDatabase"/> or the container, as the
/// DBA creates it in an install.
/// </summary>
internal static class TestMigrations
{
    /// <summary>
    /// Runs <c>wms-migrate</c> against <paramref name="connectionString"/> and fails the test when it does not exit 0.
    /// </summary>
    /// <returns>The tool's output.</returns>
    public static async Task<string> ApplyAsync(string provider, string connectionString, CancellationToken cancellationToken = default)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = await MigrateProgram.RunAsync(["--provider", provider, "--connection-string", connectionString], output, error, configuration: null, cancellationToken);

        Assert.True(exit == MigrateProgram.ExitOk, $"wms-migrate exited {exit}: {error}{output}");
        return output.ToString();
    }
}
