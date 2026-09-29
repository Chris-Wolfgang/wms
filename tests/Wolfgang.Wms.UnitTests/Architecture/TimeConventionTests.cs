// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Reflection;

namespace Wolfgang.Wms.UnitTests.Architecture;

/// <summary>
/// E1.14: UTC <see cref="DateTimeOffset"/> everywhere except display. No public member of a product assembly
/// exposes <see cref="DateTime"/>, whose offset is a guess the reader has to make.
/// </summary>
public sealed class TimeConventionTests
{
    private static readonly string[] ProductAssemblies =
    [
        "Wolfgang.Wms.Domain",
        "Wolfgang.Wms.Core",
        "Wolfgang.Wms.Api",
    ];



    [Fact]
    public void Product_assemblies_expose_no_DateTime_on_their_public_surface()
    {
        var publicTypes = ProductAssemblies
            .Select(Assembly.Load)
            .SelectMany(a => a.GetExportedTypes());

        Assert.NotEmpty(publicTypes);
        Assert.Empty(DateTimeExposuresIn(publicTypes));
    }



    [Fact]
    public void DateTime_scan_reports_properties_parameters_returns_and_generic_arguments()
    {
        var exposures = DateTimeExposuresIn([typeof(UsesDateTimeSample), typeof(UsesDateTimeOffsetSample)]);

        Assert.Equal
        (
            [
                "Wolfgang.Wms.UnitTests.Architecture.UsesDateTimeSample..ctor(when)",
                "Wolfgang.Wms.UnitTests.Architecture.UsesDateTimeSample.History",
                "Wolfgang.Wms.UnitTests.Architecture.UsesDateTimeSample.Next()",
                "Wolfgang.Wms.UnitTests.Architecture.UsesDateTimeSample.Shift(start)",
                "Wolfgang.Wms.UnitTests.Architecture.UsesDateTimeSample.When",
            ],
            exposures
        );
    }



    [Fact]
    public void Sample_fixtures_behave_as_plain_values()
    {
        var when = new DateTime(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc);
        var offset = new DateTimeOffset(when);
        var dateTimeSample = new UsesDateTimeSample(when) { History = [when] };
        var offsetSample = new UsesDateTimeOffsetSample(offset, [offset, null]);

        Assert.Equal(when, dateTimeSample.Next());
        Assert.Single(dateTimeSample.History);
        Assert.Equal("2026-09-19T12:00:00.0000000Z", UsesDateTimeSample.Shift(when));
        Assert.Equal(offset, offsetSample.Next());
        Assert.Equal(2, offsetSample.History.Count);
    }



    /// <summary>
    /// One line per public property, field, method return, or parameter whose type contains
    /// <see cref="DateTime"/> (directly or as a generic argument), sorted.
    /// </summary>
    private static List<string> DateTimeExposuresIn(IEnumerable<Type> types)
    {
        const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        return types
            .SelectMany(t =>
                t.GetProperties(Public).Where(p => Contains(p.PropertyType)).Select(p => $"{t.FullName}.{p.Name}")
                .Concat(t.GetFields(Public).Where(f => Contains(f.FieldType)).Select(f => $"{t.FullName}.{f.Name}"))
                .Concat(t.GetMethods(Public).Where(m => !m.IsSpecialName && Contains(m.ReturnType)).Select(m => $"{t.FullName}.{m.Name}()"))
                .Concat(t.GetMethods(Public).Where(m => !m.IsSpecialName).SelectMany(m => m.GetParameters().Where(p => Contains(p.ParameterType)).Select(p => $"{t.FullName}.{m.Name}({p.Name})")))
                .Concat(t.GetConstructors(Public).SelectMany(c => c.GetParameters().Where(p => Contains(p.ParameterType)).Select(p => $"{t.FullName}..ctor({p.Name})"))))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }



    private static bool Contains(Type type)
    {
        if (type == typeof(DateTime))
        {
            return true;
        }

        if (type.IsByRef || type.IsArray)
        {
            return Contains(type.GetElementType()!);
        }

        return type.IsGenericType && type.GetGenericArguments().Any(Contains);
    }
}



internal sealed class UsesDateTimeSample
{
    public UsesDateTimeSample(DateTime when)
    {
        When = when;
    }



    public DateTime When { get; }

    public IReadOnlyList<DateTime> History { get; init; } = [];



    public DateTime Next()
    {
        return When;
    }

    public static string Shift(DateTime start)
    {
        return start.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    }
}



internal sealed record UsesDateTimeOffsetSample(DateTimeOffset When, IReadOnlyList<DateTimeOffset?> History)
{
    public DateTimeOffset Next()
    {
        return When;
    }
}
