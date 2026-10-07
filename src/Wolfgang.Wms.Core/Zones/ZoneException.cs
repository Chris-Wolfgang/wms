// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Zones;

/// <summary>
/// A zone operation that cannot be carried out (E16.2); <see cref="Code"/> says why and
/// <see cref="ZoneExceptionHandler"/> turns it into the problem response.
/// </summary>
public sealed class ZoneException : Exception
{
    /// <summary>
    /// Creates the exception with its code and the detail the client sees.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public ZoneException(ErrorCode code, string detail)
        : base(detail)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// Creates the exception with its code, the detail the client sees and the cause.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public ZoneException(ErrorCode code, string detail, Exception innerException)
        : base(detail, innerException)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// Creates a <see cref="ZoneErrorCodes.Unavailable"/> exception.
    /// </summary>
    public ZoneException()
        : this(ZoneErrorCodes.Unavailable, "Zones are unavailable.")
    {
    }



    /// <summary>
    /// Creates a <see cref="ZoneErrorCodes.Unavailable"/> exception with a message.
    /// </summary>
    public ZoneException(string message)
        : this(ZoneErrorCodes.Unavailable, message)
    {
    }



    /// <summary>
    /// Creates a <see cref="ZoneErrorCodes.Unavailable"/> exception with a message and a cause.
    /// </summary>
    public ZoneException(string message, Exception innerException)
        : this(ZoneErrorCodes.Unavailable, message, innerException)
    {
    }



    /// <summary>The error code the client sees.</summary>
    public ErrorCode Code { get; }
}
