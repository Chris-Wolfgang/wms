// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Reflection;
using System.Reflection.Emit;

namespace Wolfgang.Wms.UnitTests.Architecture;

/// <summary>
/// E1.4 / E1.7: Domain is shared with the handheld and must stay pure — no I/O, no network, no data access,
/// no processes, no async — so a rule the device applies is exactly the rule the server applies.
/// </summary>
public sealed class DomainPurityTests
{
    private static readonly string[] AllowedReferencePrefixes =
    [
        "System.Runtime",
        "System.Collections",
        "System.Linq",
        "System.Memory",
        "Wolfgang.TryPattern",
    ];

    private static readonly string[] ForbiddenCallNamespaces =
    [
        "System.IO",
        "System.Net",
        "System.Data",
        "System.Diagnostics.Process",
        "System.Threading.Tasks",
    ];



    [Fact]
    public void Domain_references_only_allow_listed_assemblies()
    {
        Assert.Empty(NonAllowListedReferencesOf(DomainAssembly()));
    }



    [Fact]
    public void Reference_check_reports_the_test_frameworks_this_assembly_uses()
    {
        var found = NonAllowListedReferencesOf(typeof(DomainPurityTests).Assembly);

        Assert.Contains("xunit.core", found);
    }



    [Fact]
    public void Domain_has_no_async_members()
    {
        Assert.Empty(AsyncMembersIn(OwnTypesOf(DomainAssembly())));
    }



    [Fact]
    public void Async_member_scan_finds_task_and_value_task_returning_methods()
    {
        var found = AsyncMembersIn([typeof(ImpureSample), typeof(ISampleContract)]);

        Assert.Equal
        (
            [
                "Wolfgang.Wms.UnitTests.Architecture.ISampleContract.RunAsync",
                "Wolfgang.Wms.UnitTests.Architecture.ImpureSample.Delay",
                "Wolfgang.Wms.UnitTests.Architecture.ImpureSample.Value",
            ],
            found
        );
    }



    [Fact]
    public void Domain_makes_no_calls_into_io_network_data_process_or_task_namespaces()
    {
        var offending = ForbiddenCallsIn(OwnTypesOf(DomainAssembly()));

        Assert.Empty(offending);
    }



    [Fact]
    public async Task Forbidden_call_scan_finds_file_system_and_task_calls_in_a_sample()
    {
        var found = ForbiddenCallsIn([typeof(ImpureSample), typeof(ISampleContract)]);

        Assert.Equal
        (
            [
                "Wolfgang.Wms.UnitTests.Architecture.ImpureSample.CheckFile -> System.IO.File.Exists",
                "Wolfgang.Wms.UnitTests.Architecture.ImpureSample.Delay -> System.Threading.Tasks.Task.Delay",
                "Wolfgang.Wms.UnitTests.Architecture.ImpureSample.Value -> System.Threading.Tasks.ValueTask.FromResult",
            ],
            found
        );
        Assert.False(ImpureSample.CheckFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        Assert.Equal([0, 1, 2, 3, 4, -1], Enumerable.Range(0, 6).Select(ImpureSample.Classify));
        Assert.True(ImpureSample.Delay().IsCompleted);
        Assert.Equal(7, await ImpureSample.Value());
    }



    /// <summary>
    /// Coverage collection (coverlet) rewrites the assembly under test: it injects a
    /// <c>Coverlet.Core.Instrumentation.Tracker</c> type that does file I/O and adds the references it needs.
    /// None of that is Domain's code, so the scans skip it.
    /// </summary>
    private static readonly string[] InstrumentationReferences = ["System.Threading", "System.Threading.Thread"];



    private static Assembly DomainAssembly()
    {
        return Assembly.Load("Wolfgang.Wms.Domain");
    }



    private static IEnumerable<Type> OwnTypesOf(Assembly assembly)
    {
        return assembly.GetTypes().Where(t => !IsInjectedByCoverage(t));
    }



    private static bool IsInjectedByCoverage(Type type)
    {
        return type.FullName?.StartsWith("Coverlet.", StringComparison.Ordinal) == true;
    }



    private static List<string> NonAllowListedReferencesOf(Assembly assembly)
    {
        var instrumented = assembly.GetTypes().Any(IsInjectedByCoverage);
        return assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => !AllowedReferencePrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal))
                        && !(instrumented && InstrumentationReferences.Contains(name, StringComparer.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToList();
    }



    private static List<string> AsyncMembersIn(IEnumerable<Type> types)
    {
        return types
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => IsTaskLike(m.ReturnType))
            .Select(m => $"{m.DeclaringType!.FullName}.{m.Name}")
            .Order(StringComparer.Ordinal)
            .ToList();
    }



    private static bool IsTaskLike(Type type)
    {
        var open = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        return open == typeof(Task) || open == typeof(Task<>) || open == typeof(ValueTask) || open == typeof(ValueTask<>);
    }



    /// <summary>
    /// Every call, callvirt, newobj or ldftn in the given types whose target lives in a forbidden namespace,
    /// as "Caller -> Callee", sorted.
    /// </summary>
    private static List<string> ForbiddenCallsIn(IEnumerable<Type> types)
    {
        return types
            .SelectMany(IlCallScanner.CallsIn)
            .Where(c => c.Callee.DeclaringType?.Namespace is { } ns
                        && ForbiddenCallNamespaces.Any(f => ns.StartsWith(f, StringComparison.Ordinal)))
            .Select(c => $"{c.Caller.DeclaringType!.FullName}.{c.Caller.Name} -> {c.Callee.DeclaringType!.FullName}.{c.Callee.Name}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}



/// <summary>
/// Walks method bodies and reports the methods they call, using only System.Reflection (no Cecil dependency).
/// </summary>
internal static class IlCallScanner
{
    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(OpCode))
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => o.Value);

    private static readonly Dictionary<OperandType, int> OperandSizes = new()
    {
        [OperandType.InlineNone] = 0,
        [OperandType.ShortInlineBrTarget] = 1,
        [OperandType.ShortInlineI] = 1,
        [OperandType.ShortInlineVar] = 1,
        [OperandType.InlineVar] = 2,
        [OperandType.InlineBrTarget] = 4,
        [OperandType.InlineField] = 4,
        [OperandType.InlineI] = 4,
        [OperandType.InlineMethod] = 4,
        [OperandType.InlineSig] = 4,
        [OperandType.InlineString] = 4,
        [OperandType.InlineTok] = 4,
        [OperandType.InlineType] = 4,
        [OperandType.ShortInlineR] = 4,
        [OperandType.InlineI8] = 8,
        [OperandType.InlineR] = 8,
    };



    public static IEnumerable<(MethodBase Caller, MethodBase Callee)> CallsIn(Type type)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var members = type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all));
        return members.SelectMany(CallsIn);
    }



    private static IEnumerable<(MethodBase Caller, MethodBase Callee)> CallsIn(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            yield break;
        }

        var typeArguments = method.DeclaringType!.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        var position = 0;
        while (position < il.Length)
        {
            var opCode = ReadOpCode(il, ref position);
            if (opCode.OperandType == OperandType.InlineSwitch)
            {
                var targets = BitConverter.ToInt32(il, position);
                position += 4 + (4 * targets);
                continue;
            }

            if (opCode.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(il, position);
                yield return (method, method.Module.ResolveMethod(token, typeArguments, methodArguments)!);
            }

            position += OperandSizes[opCode.OperandType];
        }
    }



    private static OpCode ReadOpCode(byte[] il, ref int position)
    {
        short value = il[position++];
        if (value == 0xFE)
        {
            value = (short)((0xFE << 8) | il[position++]);
        }

        return OpCodesByValue[value];
    }
}



/// <summary>
/// Interface member with no body: exercises the scanner's "no IL" path and the async-member scan.
/// </summary>
internal interface ISampleContract
{
    Task RunAsync();
}



/// <summary>
/// Positive-control fixture for <see cref="DomainPurityTests"/>: file I/O, an awaitable, a two-byte opcode
/// (<c>ceq</c>) and a jump-table <c>switch</c>, so every path of the IL walker is exercised.
/// </summary>
internal static class ImpureSample
{
    internal static bool CheckFile(string path)
    {
        return File.Exists(path);
    }



    internal static Task Delay()
    {
        return Task.Delay(0);
    }



    internal static ValueTask<int> Value()
    {
        return ValueTask.FromResult(7);
    }



    internal static int Classify(int value)
    {
        var isThree = value == 3;
        return value switch
        {
            0 => 0,
            1 => 1,
            2 => 2,
            3 => isThree ? 3 : 0,
            4 => 4,
            _ => -1,
        };
    }
}
