// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// Reads the <c>static readonly</c> key instances off a definitions class, so registries (settings pages,
/// the permission catalog, the error reference) are built by enumerating the one place each key is defined.
/// </summary>
public static class KeyDefinitions
{
    /// <summary>
    /// Every public static field or property of type <typeparamref name="TKey"/> declared on
    /// <paramref name="definitions"/>, in declaration order.
    /// </summary>
    /// <exception cref="InvalidOperationException">Two members define keys with the same name.</exception>
    public static IReadOnlyList<TKey> Enumerate<TKey>
    (
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
        Type definitions
    )
        where TKey : class
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var fields = definitions
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => typeof(TKey).IsAssignableFrom(f.FieldType))
            .Select(f => (Member: (MemberInfo)f, Value: f.GetValue(null) as TKey));
        var properties = definitions
            .GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(p => typeof(TKey).IsAssignableFrom(p.PropertyType) && p.GetIndexParameters().Length == 0)
            .Select(p => (Member: (MemberInfo)p, Value: p.GetValue(null) as TKey));

        var keys = fields
            .Concat(properties)
            .OrderBy(x => x.Member.MetadataToken)
            .Select(x => x.Value ?? throw new InvalidOperationException($"{definitions.Name}.{x.Member.Name} is null; key definitions must be initialised."))
            .ToList();

        var duplicate = keys
            .GroupBy(KeyNameOf, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"{definitions.Name} defines '{duplicate.Key}' more than once.");
        }

        return keys;
    }



    private static string KeyNameOf<TKey>(TKey key)
        where TKey : class
    {
        return key switch
        {
            FeatureFlag f => f.Name,
            SettingKey s => s.Name,
            Permission p => p.Name,
            LicenseLimit l => l.Name,
            LicenseFeature l => l.Name,
            ErrorCode e => e.Code,
            IssueType i => i.Name,
            JobName j => j.Name,
            _ => key.ToString() ?? string.Empty,
        };
    }
}
