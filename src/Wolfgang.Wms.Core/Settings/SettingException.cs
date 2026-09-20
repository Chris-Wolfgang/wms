// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// A settings read or write the registry or the store refuses (E6.3), carrying the error code the API
/// answers with (<see cref="SettingExceptionHandler"/>).
/// </summary>
public sealed class SettingException : Exception
{
    /// <summary>
    /// Creates the exception for <paramref name="code"/> with a user-facing detail.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public SettingException(ErrorCode code, string detail)
        : base(detail)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// Creates the exception for <paramref name="code"/> with a user-facing detail and a cause.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public SettingException(ErrorCode code, string detail, Exception innerException)
        : base(detail, innerException)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// The error code the API answers with.
    /// </summary>
    public ErrorCode Code { get; }
}
