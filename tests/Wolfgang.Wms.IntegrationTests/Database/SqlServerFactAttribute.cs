// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// A test that needs a SQL Server (E13.1): served by Docker or by the instance
/// <see cref="SqlServerTestDatabase.EnvironmentVariable"/> names. Skipped on a developer machine with
/// neither; never skipped in CI (a runner without either is a misconfiguration and fails loudly).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
[ExcludeFromCodeCoverage]   // which branch runs depends on the machine, like DockerFactAttribute
public sealed class SqlServerFactAttribute : FactAttribute
{
    /// <summary>
    /// Sets <see cref="FactAttribute.Skip"/> when no SQL Server can serve the test outside CI.
    /// </summary>
    public SqlServerFactAttribute()
    {
        // In CI the test always runs: a runner with neither Docker nor WMS_TEST_SQLSERVER (a broken LocalDB step) is a
        // misconfiguration, and the test fails loudly inside SqlServerTestDatabase instead of passing by skipping.
        if (DockerFactAttribute.IsCi || DockerFactAttribute.DockerIsReachable || SqlServerTestDatabase.InstanceIsConfigured)
        {
            return;
        }

        Skip = "Neither Docker nor WMS_TEST_SQLSERVER is available on this machine; CI runs this test — " + DockerFactAttribute.TrackingIssue;
    }
}
