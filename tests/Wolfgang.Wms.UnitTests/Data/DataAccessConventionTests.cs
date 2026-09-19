// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Linq.Expressions;
using System.Reflection;

namespace Wolfgang.Wms.UnitTests.Data;

/// <summary>
/// E1.11 (ADR 0002): repository and unit-of-work contracts never expose the provider — no
/// <c>IQueryable</c>, no <c>DbContext</c>, no expression trees — and handlers never take a context.
/// </summary>
public sealed class DataAccessConventionTests
{
    private static readonly string[] ForbiddenTypePrefixes =
    [
        "System.Linq.IQueryable",
        "System.Linq.IOrderedQueryable",
        "System.Linq.Expressions.",
        "Microsoft.EntityFrameworkCore.",
    ];



    [Fact]
    public void Data_contracts_in_Core_expose_no_queryables_expressions_or_EF_types()
    {
        var contracts = Assembly.Load("Wolfgang.Wms.Core")
            .GetTypes()
            .Where(t => t.IsInterface && string.Equals(t.Namespace, "Wolfgang.Wms.Core.Data", StringComparison.Ordinal));

        Assert.NotEmpty(contracts);
        Assert.Empty(ProviderLeaksIn(contracts));
    }



    [Fact]
    public void Handlers_in_feature_folders_never_take_a_context()
    {
        var featureTypes = new[] { "Wolfgang.Wms.Core", "Wolfgang.Wms.Api" }
            .Select(Assembly.Load)
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Namespace?.Contains(".Features.", StringComparison.Ordinal) == true);

        Assert.Empty(ContextDependenciesIn(featureTypes));
    }



    [Fact]
    public void Provider_leak_scan_finds_a_queryable_return_an_expression_parameter_and_a_context_dependency()
    {
        var leaks = ProviderLeaksIn([typeof(ILeakySampleRepository)]);
        var dependencies = ContextDependenciesIn([typeof(LeakySampleHandler)]);
        var handler = new LeakySampleHandler(new FakeDbContext(), "sample");

        Assert.Equal
        (
            [
                "Wolfgang.Wms.UnitTests.Data.ILeakySampleRepository.All -> System.Linq.IQueryable`1",
                "Wolfgang.Wms.UnitTests.Data.ILeakySampleRepository.Where -> System.Linq.Expressions.Expression`1",
            ],
            leaks
        );
        Assert.Equal(["Wolfgang.Wms.UnitTests.Data.LeakySampleHandler(context: Wolfgang.Wms.UnitTests.Data.FakeDbContext)"], dependencies);
        Assert.Empty(ProviderLeaksIn([typeof(ICleanSampleRepository)]));
        Assert.NotNull(handler.Context);
        Assert.Equal("sample", handler.Name);
    }



    /// <summary>
    /// "Interface.Member -> offending type" for every method whose return type or parameter types touch a
    /// forbidden type, including generic arguments.
    /// </summary>
    private static List<string> ProviderLeaksIn(IEnumerable<Type> contracts)
    {
        return contracts
            .SelectMany(t => t.GetMethods().Select(m => (Type: t, Method: m)))
            .SelectMany(x => new[] { x.Method.ReturnType }
                .Concat(x.Method.GetParameters().Select(p => p.ParameterType))
                .SelectMany(Flatten)
                .Where(IsForbidden)
                .Select(f => $"{x.Type.FullName}.{x.Method.Name} -> {NameOf(f)}"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }



    /// <summary>
    /// "Handler(parameter: type)" for every constructor parameter whose type is, or derives from, a type in a
    /// forbidden namespace or is named like a database context.
    /// </summary>
    private static List<string> ContextDependenciesIn(IEnumerable<Type> handlers)
    {
        return handlers
            .SelectMany(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(c => c.GetParameters())
                .Where(p => IsContextLike(p.ParameterType))
                .Select(p => $"{t.FullName}({p.Name}: {p.ParameterType.FullName})"))
            .Order(StringComparer.Ordinal)
            .ToList();
    }



    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments().SelectMany(Flatten))
            {
                yield return argument;
            }
        }
    }



    private static bool IsForbidden(Type type)
    {
        var name = NameOf(type);
        return ForbiddenTypePrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));
    }



    /// <summary>
    /// Open-generic full name (<c>System.Linq.IQueryable`1</c>) so constructed generics read the same as their
    /// definition; generic parameters have no full name and fall back to their short name.
    /// </summary>
    private static string NameOf(Type type)
    {
        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        return definition.FullName ?? definition.Name;
    }



    private static bool IsContextLike(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (IsForbidden(current) || current.Name.EndsWith("DbContext", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}



internal interface ILeakySampleRepository
{
    IQueryable<string> All();

    Task<string?> Where(Expression<Func<string, bool>> predicate, CancellationToken cancellationToken);
}



internal interface ICleanSampleRepository
{
    Task<IReadOnlyList<string>> SearchAsync(string criteria, CancellationToken cancellationToken);
}



internal sealed class FakeDbContext
{
}



internal sealed class LeakySampleHandler
{
    public LeakySampleHandler(FakeDbContext context, string name)
    {
        Context = context;
        Name = name;
    }



    public FakeDbContext Context { get; }



    public string Name { get; }
}
