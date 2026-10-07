// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Imports;

/// <summary>
/// An import request that cannot be carried out at all (E16.6); <see cref="Code"/> says why and
/// <see cref="ImportExceptionHandler"/> turns it into the problem response. Row failures are not exceptions:
/// they are reported in the <see cref="ImportResult"/>.
/// </summary>
public sealed class ImportException : Exception
{
    /// <summary>
    /// Creates the exception with its code and the detail the client sees.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public ImportException(ErrorCode code, string detail)
        : base(detail)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// Creates the exception with its code, the detail the client sees and the cause.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public ImportException(ErrorCode code, string detail, Exception innerException)
        : base(detail, innerException)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// Creates an <see cref="ImportErrorCodes.Unavailable"/> exception.
    /// </summary>
    public ImportException()
        : this(ImportErrorCodes.Unavailable, "Imports are unavailable.")
    {
    }



    /// <summary>
    /// Creates an <see cref="ImportErrorCodes.Unavailable"/> exception with a message.
    /// </summary>
    public ImportException(string message)
        : this(ImportErrorCodes.Unavailable, message)
    {
    }



    /// <summary>
    /// Creates an <see cref="ImportErrorCodes.Unavailable"/> exception with a message and a cause.
    /// </summary>
    public ImportException(string message, Exception innerException)
        : this(ImportErrorCodes.Unavailable, message, innerException)
    {
    }



    /// <summary>The error code the client sees.</summary>
    public ErrorCode Code { get; }
}
