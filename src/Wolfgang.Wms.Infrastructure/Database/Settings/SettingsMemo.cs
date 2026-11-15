// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Collections.Concurrent;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Infrastructure.Database.Settings;

/// <summary>
/// One generation of what <see cref="CachedSettings"/> has resolved: typed values, DTOs and scope lists, each
/// filled on first use. <see cref="SettingsCache"/> publishes a fresh, empty generation whenever the settings
/// or the hierarchy tables move (ADR 0003), and a generation that was superseded while a read was in flight
/// may still receive that read's result, which is harmless because nothing serves it again.
/// </summary>
public sealed class SettingsMemo
{
    /// <summary>
    /// Typed values by scope and key, boxed; a secret is held as the store returned it (ciphertext).
    /// </summary>
    public ConcurrentDictionary<(SettingScopeRef Scope, string Key), object> Typed { get; } = new();



    /// <summary>
    /// The DTO of one setting at a scope.
    /// </summary>
    public ConcurrentDictionary<(SettingScopeRef Scope, string Key), SettingValue> Values { get; } = new();



    /// <summary>
    /// Every setting at a scope.
    /// </summary>
    public ConcurrentDictionary<SettingScopeRef, IReadOnlyList<SettingValue>> Lists { get; } = new();



    /// <summary>
    /// How many entries this generation holds.
    /// </summary>
    public int Count => Typed.Count + Values.Count + Lists.Count;
}
