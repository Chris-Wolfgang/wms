// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Identity;

/// <summary>
/// A sign-in or account operation the store refuses (E9), carrying the error code the API answers with
/// (<see cref="AuthExceptionHandler"/>).
/// </summary>
public sealed class AuthException : Exception
{
    /// <summary>
    /// Creates the exception for <paramref name="code"/> with a user-facing detail.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public AuthException(ErrorCode code, string detail)
        : base(detail)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// Creates the exception for <paramref name="code"/> with a user-facing detail and a cause.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public AuthException(ErrorCode code, string detail, Exception innerException)
        : base(detail, innerException)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// The error code the API answers with.
    /// </summary>
    public ErrorCode Code { get; }
}
