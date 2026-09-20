// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// A fact that needs a container runtime. On the Linux CI job Docker is present and it always runs; on the
/// Windows CI job (no Docker daemon) it is skipped with a reason naming the tracking issue (E13.1: every
/// skip names an open issue; <c>scripts/Check-Skips.ps1</c> enforces it); on a developer machine without a
/// reachable Docker engine it is skipped the same way. SQL Server tests use <see cref="SqlServerFactAttribute"/>
/// instead, which also runs against a local instance.
/// </summary>
/// <remarks>
/// Excluded from coverage for the same reason coverlet.runsettings excludes <c>*Fixture</c> classes: which
/// branch runs depends on the machine (the skip branch never executes where Docker is present, the run branch
/// never executes where it is not), so no single environment can cover it.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
[ExcludeFromCodeCoverage]
public sealed class DockerFactAttribute : FactAttribute
{
    /// <summary>
    /// The issue every Docker-dependent skip names.
    /// </summary>
    public const string TrackingIssue = "Chris-Wolfgang/wms#220";



    /// <summary>
    /// Sets <see cref="FactAttribute.Skip"/> when Docker is unavailable.
    /// </summary>
    public DockerFactAttribute()
    {
        if (DockerIsReachable)
        {
            return;
        }

        Skip = IsCi
            ? "Docker is not available on this runner (the Windows job); the Linux job runs this test — " + TrackingIssue
            : "Docker is not available on this machine; CI runs this test — " + TrackingIssue;
    }



    /// <summary>
    /// True on a GitHub Actions runner.
    /// </summary>
    public static bool IsCi => string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);



    /// <summary>
    /// True when a Docker engine looks reachable from this process.
    /// </summary>
    public static bool DockerIsReachable =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST"))
        || File.Exists("/var/run/docker.sock")
        || File.Exists(@"\\.\pipe\docker_engine")
        || File.Exists(@"\\.\pipe\dockerDesktopLinuxEngine");
}
