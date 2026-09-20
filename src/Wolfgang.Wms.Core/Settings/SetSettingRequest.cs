// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// Body of <c>PUT /settings/{scope}/{id}/{key}</c> (E6.3): the value as stored text. Null resets the scope
/// to inherit (the same as <c>DELETE</c>).
/// </summary>
/// <param name="Value">The stored text, or null to reset.</param>
public sealed record SetSettingRequest(string? Value);
