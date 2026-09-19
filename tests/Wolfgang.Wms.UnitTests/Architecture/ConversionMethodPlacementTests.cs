// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wolfgang.Wms.UnitTests.Architecture;

/// <summary>
/// E1.3: conversion extension methods are named for the lower-level type and defined in the higher layer that
/// knows both types. <c>ToDto</c>/<c>FromDto</c> belong to Api, <c>ToEntity</c>/<c>FromEntity</c> to
/// Infrastructure, and Domain defines neither.
/// </summary>
public sealed class ConversionMethodPlacementTests
{
    private static readonly string[] DtoConversions = ["ToDto", "FromDto"];
    private static readonly string[] EntityConversions = ["ToEntity", "FromEntity"];



    [Fact]
    public void Domain_defines_no_conversion_methods_at_all()
    {
        var offending = ConversionMethodsIn(Assembly.Load("Wolfgang.Wms.Domain"), [.. DtoConversions, .. EntityConversions]);

        Assert.Empty(offending);
    }



    [Fact]
    public void Infrastructure_defines_no_dto_conversions()
    {
        var offending = ConversionMethodsIn(Assembly.Load("Wolfgang.Wms.Infrastructure"), DtoConversions);

        Assert.Empty(offending);
    }



    [Fact]
    public void Api_defines_no_entity_conversions()
    {
        var offending = ConversionMethodsIn(Assembly.Load("Wolfgang.Wms.Api"), EntityConversions);

        Assert.Empty(offending);
    }



    [Fact]
    public void Conversion_method_scan_finds_a_misplaced_extension_method()
    {
        var found = ConversionMethodsIn(typeof(ConversionMethodPlacementTests).Assembly, ["ToDto"]);

        Assert.Equal(["Wolfgang.Wms.UnitTests.Architecture.MisplacedConversionSample.ToDto"], found);
        Assert.Equal("5", 5.ToDto());
    }



    /// <summary>
    /// Every extension method in <paramref name="assembly"/> whose name starts with one of
    /// <paramref name="prefixes"/>, as <c>Namespace.Type.Method</c>.
    /// </summary>
    private static List<string> ConversionMethodsIn(Assembly assembly, string[] prefixes)
    {
        return assembly
            .GetTypes()
            .Where(t => t.IsSealed && t.IsAbstract && t.IsDefined(typeof(ExtensionAttribute), inherit: false))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(m => m.IsDefined(typeof(ExtensionAttribute), inherit: false)
                     && prefixes.Any(p => m.Name.StartsWith(p, StringComparison.Ordinal)))
            .Select(m => $"{m.DeclaringType!.FullName}.{m.Name}")
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}



/// <summary>
/// Positive-control fixture for <see cref="ConversionMethodPlacementTests"/>: a conversion method in the wrong
/// place, so the scan is proven to detect one.
/// </summary>
internal static class MisplacedConversionSample
{
    internal static string ToDto(this int value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
