// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Locations;

/// <summary>
/// A location operation that cannot be carried out (E17.1); <see cref="Code"/> says why and
/// <see cref="LocationExceptionHandler"/> turns it into the problem response.
/// </summary>
public sealed class LocationException : Exception
{
    /// <summary>
    /// Creates the exception with its code and the detail the client sees.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public LocationException(ErrorCode code, string detail)
        : base(detail)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// Creates the exception with its code, the detail the client sees and the cause.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public LocationException(ErrorCode code, string detail, Exception innerException)
        : base(detail, innerException)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// Creates a <see cref="LocationErrorCodes.Unavailable"/> exception.
    /// </summary>
    public LocationException()
        : this(LocationErrorCodes.Unavailable, "Locations are unavailable.")
    {
    }



    /// <summary>
    /// Creates a <see cref="LocationErrorCodes.Unavailable"/> exception with a message.
    /// </summary>
    public LocationException(string message)
        : this(LocationErrorCodes.Unavailable, message)
    {
    }



    /// <summary>
    /// Creates a <see cref="LocationErrorCodes.Unavailable"/> exception with a message and a cause.
    /// </summary>
    public LocationException(string message, Exception innerException)
        : this(LocationErrorCodes.Unavailable, message, innerException)
    {
    }



    /// <summary>The error code the client sees.</summary>
    public ErrorCode Code { get; }
}
