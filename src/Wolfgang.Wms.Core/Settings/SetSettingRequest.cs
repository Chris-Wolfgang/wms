// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// Body of <c>PUT /settings/{scope}/{id}/{key}</c> (E6.3, E7.2): a value as stored text, or a cascade mode
/// (<c>per_site</c>, <c>per_zone</c>, <c>per_sku</c>) that delegates the decision downward. Both null resets
/// the scope to inherit (the same as <c>DELETE</c>); mode <c>value</c> alone does the same.
/// </summary>
/// <param name="Value">The stored text, or null.</param>
/// <param name="Mode">The cascade mode's stored name, or null to keep or set a value.</param>
public sealed record SetSettingRequest(string? Value, string? Mode = null);
