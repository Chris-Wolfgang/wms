// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// Untyped view of a setting key, for catalogs and registries that list settings of every type (E1.13,
/// E6.1). Everything a settings page or the generated documentation shows comes from here: kind, allowed
/// scopes, default, description, and whether a change needs a restart or a device resync.
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



    /// <summary>
    /// The scopes the setting may be configured at; a chain from the organisation down. Defaults to
    /// organisation → site → zone.
    /// </summary>
    public SettingScopes Scopes { get; init; } = SettingScopes.OrganizationToZone;



    /// <summary>
    /// True when a change takes effect only after the host restarts (the console says so when saving).
    /// </summary>
    public bool RequiresRestart { get; init; }



    /// <summary>
    /// True when a change must reach devices before they continue (they resync their settings cache).
    /// </summary>
    public bool TriggersDeviceResync { get; init; }



    /// <summary>
    /// How the value is edited and stored.
    /// </summary>
    public abstract SettingKind Kind { get; }



    /// <summary>
    /// The allowed texts when the kind is a fixed list, else null.
    /// </summary>
    public abstract IReadOnlyList<string>? Choices { get; }



    /// <summary>
    /// The default value as stored text.
    /// </summary>
    public abstract string DefaultText { get; }



    /// <summary>
    /// Checks stored text against the kind and the key's validator: null when it is a valid value, else a
    /// user-facing reason.
    /// </summary>
    public abstract string? Validate(string? text);



    /// <summary>
    /// True when the key may be configured at <paramref name="scope"/>.
    /// </summary>
    public bool AllowsScope(SettingScope scope)
    {
        return Scopes.Allows(scope);
    }
}



/// <summary>
/// A typed setting key. <c>ISettings.Get(SettingKeys.LeaseTimeout, scope)</c> infers <typeparamref name="T"/>
/// from the key, so a setting can never be read as the wrong type or under a misspelt name.
/// </summary>
/// <typeparam name="T">Value type of the setting.</typeparam>
public sealed record SettingKey<T> : SettingKey
{
    /// <summary>
    /// Creates a typed setting key with its default value, using the built-in codec for <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> has no built-in codec; use the overload that takes one.</exception>
    public SettingKey(string name, T defaultValue, string description)
        : this(name, defaultValue, description, SettingCodecs.For<T>())
    {
    }



    /// <summary>
    /// Creates a typed setting key with its default value and the codec that stores it.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="codec"/> is null.</exception>
    public SettingKey(string name, T defaultValue, string description, SettingCodec<T> codec)
        : base(name, typeof(T), description)
    {
        DefaultValue = defaultValue;
        Codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }



    /// <summary>
    /// Value used when nothing more specific is configured at any scope.
    /// </summary>
    public T DefaultValue { get; }



    /// <summary>
    /// Converts the value to and from stored text.
    /// </summary>
    public SettingCodec<T> Codec { get; }



    /// <summary>
    /// Rejects values the kind alone cannot: returns null for a valid value, else a user-facing reason
    /// (<c>"must be between 1 and 60 minutes"</c>).
    /// </summary>
    public Func<T, string?>? Validator { get; init; }



    /// <inheritdoc/>
    public override SettingKind Kind => Codec.Kind;



    /// <inheritdoc/>
    public override IReadOnlyList<string>? Choices => Codec.Choices;



    /// <inheritdoc/>
    public override string DefaultText => Codec.Format(DefaultValue);



    /// <inheritdoc/>
    public override string? Validate(string? text)
    {
        if (!Codec.TryParse(text, out var value))
        {
            return $"'{text}' is not a valid {Kind.ToString().ToLowerInvariant()} value for {Name}.";
        }

        return Validate(value);
    }



    /// <summary>
    /// Runs the key's validator on a typed value: null when valid, else the reason.
    /// </summary>
    public string? Validate(T value)
    {
        return Validator?.Invoke(value);
    }
}
