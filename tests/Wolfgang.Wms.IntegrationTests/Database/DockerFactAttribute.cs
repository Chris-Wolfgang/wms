// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// A fact that needs a container runtime. On the Linux CI job Docker is present and it always runs; on the
/// Windows CI job (no engine that runs Linux containers) it is skipped with a reason naming the tracking
/// issue (every skip names an open issue); on a developer machine without a reachable Docker engine it is
/// skipped the same way.
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



    private static readonly Lazy<bool> LinuxEngine = new(ProbeLinuxEngine);



    /// <summary>
    /// True when a Docker engine answers and runs Linux containers (the images these tests pull). Probed once:
    /// a stale <c>DOCKER_HOST</c>, a stopped Docker Desktop or a Windows-containers engine (the Windows CI
    /// runner) all count as unavailable, so the test is skipped instead of failing in <c>StartAsync</c>.
    /// </summary>
    public static bool DockerIsReachable => LinuxEngine.Value;



    private static bool ProbeLinuxEngine()
    {
        try
        {
            // `docker info` talks to the engine DOCKER_HOST / the current context points at, as Testcontainers does.
            using var process = Process.Start
            (
                new ProcessStartInfo("docker", "info --format {{.OSType}}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            );
            if (process is null || !process.WaitForExit(TimeSpan.FromSeconds(15)))
            {
                process?.Kill(entireProcessTree: true);
                return false;
            }

            return process.ExitCode == 0
                && string.Equals(process.StandardOutput.ReadToEnd().Trim(), "linux", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;   // no docker CLI, or it could not start: skip
        }
    }
}
