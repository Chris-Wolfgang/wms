// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// Error codes of the settings module (E6.1, E6.3).
/// </summary>
public static class SettingErrorCodes
{
    /// <summary>
    /// A write named a setting no module declares.
    /// </summary>
    public static ErrorCode UnknownKey { get; } = new
    (
        "settings.unknown_key",
        StatusCodes.Status404NotFound,
        "'{0}' is not a registered setting.",
        "settings-unknown-key",
        ErrorSeverity.Error
    );



    /// <summary>
    /// A write targeted a scope the setting cannot be configured at.
    /// </summary>
    public static ErrorCode ScopeNotAllowed { get; } = new
    (
        "settings.scope_not_allowed",
        StatusCodes.Status400BadRequest,
        "{0} cannot be configured at the {1} scope.",
        "settings-scope-not-allowed",
        ErrorSeverity.Error
    );



    /// <summary>
    /// A write carried a value the setting's kind or validator rejects.
    /// </summary>
    public static ErrorCode InvalidValue { get; } = new
    (
        "settings.invalid_value",
        StatusCodes.Status400BadRequest,
        "{0}",
        "settings-invalid-value",
        ErrorSeverity.Error
    );
}
