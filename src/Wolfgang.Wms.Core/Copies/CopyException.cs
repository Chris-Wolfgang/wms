// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Copies;

/// <summary>
/// A copy that cannot be carried out (E16.5); <see cref="Code"/> says why and <see cref="CopyExceptionHandler"/>
/// turns it into the problem response.
/// </summary>
public sealed class CopyException : Exception
{
    /// <summary>
    /// Creates the exception with its code and the detail the client sees.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public CopyException(ErrorCode code, string detail)
        : base(detail)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// Creates the exception with its code, the detail the client sees and the cause.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public CopyException(ErrorCode code, string detail, Exception innerException)
        : base(detail, innerException)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// Creates a <see cref="CopyErrorCodes.Unavailable"/> exception.
    /// </summary>
    public CopyException()
        : this(CopyErrorCodes.Unavailable, "Copies are unavailable.")
    {
    }



    /// <summary>
    /// Creates a <see cref="CopyErrorCodes.Unavailable"/> exception with a message.
    /// </summary>
    public CopyException(string message)
        : this(CopyErrorCodes.Unavailable, message)
    {
    }



    /// <summary>
    /// Creates a <see cref="CopyErrorCodes.Unavailable"/> exception with a message and a cause.
    /// </summary>
    public CopyException(string message, Exception innerException)
        : this(CopyErrorCodes.Unavailable, message, innerException)
    {
    }



    /// <summary>The error code the client sees.</summary>
    public ErrorCode Code { get; }
}
