// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Sites;

/// <summary>
/// A site operation that cannot be carried out (E16.1); <see cref="Code"/> says why and
/// <see cref="SiteExceptionHandler"/> turns it into the problem response.
/// </summary>
public sealed class SiteException : Exception
{
    /// <summary>
    /// Creates the exception with its code and the detail the client sees.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public SiteException(ErrorCode code, string detail)
        : base(detail)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// Creates the exception with its code, the detail the client sees and the cause.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public SiteException(ErrorCode code, string detail, Exception innerException)
        : base(detail, innerException)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// Creates an <see cref="SiteErrorCodes.Unavailable"/> exception.
    /// </summary>
    public SiteException()
        : this(SiteErrorCodes.Unavailable, "Sites are unavailable.")
    {
    }



    /// <summary>
    /// Creates an <see cref="SiteErrorCodes.Unavailable"/> exception with a message.
    /// </summary>
    public SiteException(string message)
        : this(SiteErrorCodes.Unavailable, message)
    {
    }



    /// <summary>
    /// Creates an <see cref="SiteErrorCodes.Unavailable"/> exception with a message and a cause.
    /// </summary>
    public SiteException(string message, Exception innerException)
        : this(SiteErrorCodes.Unavailable, message, innerException)
    {
    }



    /// <summary>The error code the client sees.</summary>
    public ErrorCode Code { get; }
}
