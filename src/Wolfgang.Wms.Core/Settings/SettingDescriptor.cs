// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json.Serialization;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// One registry entry as the API and the console see it (E6.1): everything a settings page needs to render
/// and validate a setting without knowing its CLR type.
/// </summary>
/// <param name="Name">The setting name.</param>
/// <param name="Kind">How the value is edited and stored.</param>
/// <param name="Scopes">The scope names the setting may be configured at, organisation first.</param>
/// <param name="Default">The default value as stored text; masked for secrets.</param>
/// <param name="Description">The one-line description.</param>
/// <param name="Choices">The allowed values when the kind is a fixed list, else null.</param>
/// <param name="RequiresRestart">True when a change needs a host restart.</param>
/// <param name="TriggersDeviceResync">True when a change makes devices resync.</param>
public sealed record SettingDescriptor
(
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SettingKind>))] SettingKind Kind,
    IReadOnlyList<string> Scopes,
    string Default,
    string Description,
    IReadOnlyList<string>? Choices,
    bool RequiresRestart,
    bool TriggersDeviceResync
)
{
    /// <summary>
    /// The descriptor of a key. A secret's default is masked, never shown.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public static SettingDescriptor Of(SettingKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var scopes = new[] { SettingScope.Organization, SettingScope.Site, SettingScope.Zone, SettingScope.Sku }
            .Where(key.AllowsScope)
            .Select(s => s.StoredName())
            .ToList();
        var defaultText = key.Kind == SettingKind.Secret ? new SecretText(key.DefaultText).ToString() : key.DefaultText;
        return new SettingDescriptor(key.Name, key.Kind, scopes, defaultText, key.Description, key.Choices, key.RequiresRestart, key.TriggersDeviceResync);
    }
}
