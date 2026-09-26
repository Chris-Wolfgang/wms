// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// A fact that needs a container runtime. In CI (<c>GITHUB_ACTIONS</c>) it always runs, so a runner without
/// Docker fails loudly rather than silently skipping the provider tests; on a developer machine without a
/// reachable Docker engine it is skipped with a reason.
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
    /// Sets <see cref="FactAttribute.Skip"/> when Docker is unavailable outside CI.
    /// </summary>
    public DockerFactAttribute()
    {
        if (!IsCi && !DockerIsReachable)
        {
            Skip = "Docker is not available on this machine; CI runs this test.";
        }
    }



    private static bool IsCi => string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);



    private static bool DockerIsReachable =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST"))
        || File.Exists("/var/run/docker.sock")
        || File.Exists(@"\\.\pipe\docker_engine")
        || File.Exists(@"\\.\pipe\dockerDesktopLinuxEngine");
}
