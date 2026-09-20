// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json.Serialization;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// One setting at one scope as the API and the console see it (E6.3): what is configured here, what
/// applies here, and where the effective value comes from when it is inherited. Secrets are masked.
/// </summary>
/// <param name="Name">The setting name.</param>
/// <param name="Scope">The scope (<c>organization</c>, <c>site:3</c>).</param>
/// <param name="Kind">How the value is edited.</param>
/// <param name="ConfiguredValue">The text configured at this scope, or null when it inherits.</param>
/// <param name="EffectiveValue">The text that applies at this scope.</param>
/// <param name="InheritedFrom">The scope the effective value comes from (<c>organization</c>), <c>default</c> for the key's default, or null when configured here.</param>
/// <param name="CascadeMode">How this scope takes part in the cascade: <c>value</c>, or <c>per_site</c>/<c>per_zone</c>/<c>per_sku</c> when it delegates (E7.2).</param>
/// <param name="AllowedModes">The modes this key allows at this scope.</param>
/// <param name="RowVersion">The stored row's version, or null when no row exists at this scope yet.</param>
/// <param name="UpdatedBy">Who last wrote the row, if one exists.</param>
/// <param name="UpdatedAt">When the row was last written, if one exists.</param>
public sealed record SettingValue
(
    string Name,
    string Scope,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SettingKind>))] SettingKind Kind,
    string? ConfiguredValue,
    string EffectiveValue,
    string? InheritedFrom,
    string CascadeMode,
    IReadOnlyList<string> AllowedModes,
    long? RowVersion,
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt
)
{
    /// <summary>
    /// The source name reported when the effective value is the key's default.
    /// </summary>
    public const string DefaultSource = "default";



    /// <summary>
    /// The entity tag of the stored row (<c>If-Match</c> on writes), or null when no row exists.
    /// </summary>
    public string? Etag => RowVersion is > 0 ? EntityTag.FromRowVersion((ulong)RowVersion.Value).Value : null;



    /// <summary>
    /// Builds the value for <paramref name="key"/>, masking secrets.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="effectiveValue"/> is null.</exception>
    public static SettingValue Create(SettingKey key, SettingScopeRef scope, string? configuredValue, string effectiveValue, string? inheritedFrom, Domain.Settings.CascadeMode cascadeMode = Domain.Settings.CascadeMode.Value, long? rowVersion = null, string? updatedBy = null, DateTimeOffset? updatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(effectiveValue);

        var secret = key.Kind == SettingKind.Secret;
        return new SettingValue
        (
            key.Name,
            scope.ToString(),
            key.Kind,
            secret && configuredValue is not null ? Mask(configuredValue) : configuredValue,
            secret ? Mask(effectiveValue) : effectiveValue,
            inheritedFrom,
            cascadeMode.StoredName(),
            CascadeModeExtensions.AllowedAt(scope.Type, key.Scopes).Select(m => m.StoredName()).ToList(),
            rowVersion,
            updatedBy,
            updatedAt
        );
    }



    private static string Mask(string text)
    {
        return new SecretText(text).ToString();
    }
}
