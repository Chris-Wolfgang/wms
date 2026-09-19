// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// Untyped view of a setting key, for catalogs and registries that list settings of every type.
/// </summary>
public abstract record SettingKey
{
    /// <summary>
    /// Creates the untyped part of a setting key.
    /// </summary>
    protected SettingKey(string name, Type valueType, string description)
    {
        Name = KeyName.Require(name, nameof(name));
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Description = description;
    }



    /// <summary>
    /// Stable setting name, for example <c>picking.lease_timeout</c>.
    /// </summary>
    public string Name { get; }



    /// <summary>
    /// CLR type of the setting's value.
    /// </summary>
    public Type ValueType { get; }



    /// <summary>
    /// One-line, user-facing description shown on settings pages and in generated documentation.
    /// </summary>
    public string Description { get; }
}



/// <summary>
/// A typed setting key. <c>ISettings.Get(SettingKeys.LeaseTimeout, scope)</c> infers <typeparamref name="T"/>
/// from the key, so a setting can never be read as the wrong type or under a misspelt name.
/// </summary>
/// <typeparam name="T">Value type of the setting.</typeparam>
public sealed record SettingKey<T> : SettingKey
{
    /// <summary>
    /// Creates a typed setting key with its default value.
    /// </summary>
    public SettingKey(string name, T defaultValue, string description)
        : base(name, typeof(T), description)
    {
        DefaultValue = defaultValue;
    }



    /// <summary>
    /// Value used when nothing more specific is configured at any scope.
    /// </summary>
    public T DefaultValue { get; }
}
