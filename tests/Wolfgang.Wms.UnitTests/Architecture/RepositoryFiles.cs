// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Xml.Linq;

namespace Wolfgang.Wms.UnitTests.Architecture;

/// <summary>
/// Locates the repository checkout the tests run from, so architecture tests can read project files.
/// </summary>
internal static class RepositoryFiles
{
    private const string SolutionFileName = "Wolfgang.Wms.slnx";



    /// <summary>
    /// The repository root: the nearest ancestor of the test output directory that holds the solution file.
    /// </summary>
    public static string Root { get; } = FindRoot(AppContext.BaseDirectory);



    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> to the directory holding the solution file.
    /// </summary>
    /// <exception cref="InvalidOperationException">No ancestor of <paramref name="startDirectory"/> holds the solution file.</exception>
    public static string FindRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException($"{SolutionFileName} not found above {startDirectory}");
    }



    /// <summary>
    /// Every project file under the repository (source, tests, benchmarks), as repository-relative paths
    /// with forward slashes, sorted.
    /// </summary>
    public static IReadOnlyList<string> ProjectPaths()
    {
        return Directory
            .EnumerateFiles(Root, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(p => Path.GetRelativePath(Root, p).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
    }



    /// <summary>
    /// Loads a project file by repository-relative path.
    /// </summary>
    public static XDocument LoadProject(string relativeProjectPath)
    {
        return XDocument.Load(Path.Combine(Root, relativeProjectPath));
    }
}
