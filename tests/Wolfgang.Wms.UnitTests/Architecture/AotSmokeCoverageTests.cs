// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.RegularExpressions;

namespace Wolfgang.Wms.UnitTests.Architecture;

/// <summary>
/// Unit tests run under the JIT, so they cannot see code that compiles cleanly but fails under NativeAOT (MetadataToken
/// threw in KeyDefinitions). Every file in an AOT-compatible src/ project that uses reflection, JSON serialization or
/// configuration binding must therefore be exercised by the NativeAOT smoke program and listed in its
/// covered-sources.txt; this test fails the build otherwise.
/// </summary>
public sealed class AotSmokeCoverageTests
{
    private const string ManifestPath = "tests/Wolfgang.Wms.AotSmoke/covered-sources.txt";

    // E3.8 convention for every regex in the product: no backtracking and a match timeout (MA0009), explicit
    // capture only (MA0023).
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking;
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);

    private static readonly Regex[] AotSensitivePatterns =
    [
        new(@"\.(GetFields|GetProperties|GetMethods?|GetMembers?|GetConstructors?|GetInterfaces|GetTypes|GetExportedTypes)\(", Options, Timeout),
        new(@"\.GetCustomAttributes?\b", Options, Timeout),
        new(@"\bType\.GetType\(", Options, Timeout),
        new(@"\bActivator\.", Options, Timeout),
        new(@"\.(MakeGenericType|MakeGenericMethod)\(", Options, Timeout),
        new(@"\.MetadataToken\b", Options, Timeout),
        new(@"\bAssembly\.(Load|LoadFrom|GetExecutingAssembly|GetEntryAssembly)\b", Options, Timeout),
        new(@"\bExpression\.(Lambda|Compile)\b|\.Compile\(\)", Options, Timeout),
        new(@"\bEnum\.(GetValues|GetNames)\(typeof", Options, Timeout),
        new(@"\bJsonSerializer\.", Options, Timeout),
        new(@"\[(DynamicallyAccessedMembers|RequiresUnreferencedCode|RequiresDynamicCode|UnconditionalSuppressMessage)\b", Options, Timeout),
        new(@"\bConfigurationBinder\.|\.Bind\(", Options, Timeout),
    ];

    private static readonly Regex LineComment = new(@"//.*$", RegexOptions.Multiline | Options, Timeout);



    [Fact]
    public void Every_AOT_sensitive_source_file_is_covered_by_the_smoke_program()
    {
        var missing = AotSensitiveSourceFiles().Except(ManifestEntries(), StringComparer.Ordinal).ToList();

        Assert.True
        (
            missing.Count == 0,
            "These files use reflection, JSON or configuration binding in an AOT-compatible project but the NativeAOT "
            + "smoke program does not cover them. Add a check to tests/Wolfgang.Wms.AotSmoke/SmokeRunner.cs that calls "
            + $"the AOT-sensitive path, then list the file in {ManifestPath}:{Environment.NewLine}"
            + string.Join(Environment.NewLine, missing)
        );
    }



    [Fact]
    public void Every_listed_source_file_still_exists_and_still_needs_coverage()
    {
        var stale = ManifestEntries().Except(AotSensitiveSourceFiles(), StringComparer.Ordinal).ToList();

        Assert.True
        (
            stale.Count == 0,
            $"{ManifestPath} lists files that no longer exist or no longer use an AOT-sensitive API; remove them:"
            + Environment.NewLine + string.Join(Environment.NewLine, stale)
        );
    }



    [Theory]
    [InlineData("var fields = definitions.GetFields(flags);")]
    [InlineData("var token = member.MetadataToken;")]
    [InlineData("var json = JsonSerializer.Serialize(value, Context.Default.Value);")]
    [InlineData("section.Bind(options);")]
    [InlineData("var names = Enum.GetNames(typeof(Kind));")]
    [InlineData("[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]")]
    public void Detector_flags_AOT_sensitive_code(string code)
    {
        Assert.True(IsAotSensitive(code));
    }



    [Theory]
    [InlineData("var total = items.Sum(i => i.Count);")]
    [InlineData("// JsonSerializer.Serialize is mentioned only in a comment")]
    [InlineData("/// <see cref=\"MemberInfo.MetadataToken\"/> throws under NativeAOT.")]
    public void Detector_ignores_ordinary_code_and_comments(string code)
    {
        Assert.False(IsAotSensitive(code));
    }



    private static bool IsAotSensitive(string source)
    {
        var code = LineComment.Replace(source, string.Empty);
        return AotSensitivePatterns.Any(p => p.IsMatch(code));
    }



    private static List<string> AotSensitiveSourceFiles()
    {
        return AotCompatibleProjectDirectories()
            .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(RepositoryFiles.Root, dir), "*.cs", SearchOption.AllDirectories))
            .Select(p => Path.GetRelativePath(RepositoryFiles.Root, p).Replace('\\', '/'))
            .Where(p => !p.Contains("/obj/", StringComparison.Ordinal)
                        && !p.Contains("/bin/", StringComparison.Ordinal)
                        && IsAotSensitive(File.ReadAllText(Path.Combine(RepositoryFiles.Root, p))))
            .Order(StringComparer.Ordinal)
            .ToList();
    }



    // Mirrors Directory.Build.props: every src/ project except the Blazor console (.Web) and the MAUI handheld
    // (.Android) is AOT-compatible unless its csproj opts out with <IsAotCompatible>false</IsAotCompatible>.
    private static IEnumerable<string> AotCompatibleProjectDirectories()
    {
        return RepositoryFiles
            .ProjectPaths()
            .Where(p => p.StartsWith("src/", StringComparison.Ordinal)
                        && !Path.GetFileNameWithoutExtension(p).EndsWith(".Web", StringComparison.Ordinal)
                        && !Path.GetFileNameWithoutExtension(p).EndsWith(".Android", StringComparison.Ordinal)
                        && !OptsOutOfAot(p))
            .Select(p => Path.GetDirectoryName(p)!.Replace('\\', '/'));
    }



    private static bool OptsOutOfAot(string relativeProjectPath)
    {
        return RepositoryFiles
            .LoadProject(relativeProjectPath)
            .Descendants()
            .Any(e => string.Equals(e.Name.LocalName, "IsAotCompatible", StringComparison.Ordinal)
                      && string.Equals(e.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase));
    }



    private static List<string> ManifestEntries()
    {
        return File
            .ReadAllLines(Path.Combine(RepositoryFiles.Root, ManifestPath))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
