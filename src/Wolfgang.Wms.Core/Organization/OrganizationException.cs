// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Organization;

/// <summary>
/// An organisation operation the store refuses (E16.0), carrying the error code the API answers with
/// (<see cref="OrganizationExceptionHandler"/>).
/// </summary>
public sealed class OrganizationException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public OrganizationException(ErrorCode code, string detail)
        : base(detail)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// Creates the exception with a cause.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public OrganizationException(ErrorCode code, string detail, Exception innerException)
        : base(detail, innerException)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }



    /// <summary>
    /// Creates the exception with no code; for serialization and framework use only.
    /// </summary>
    public OrganizationException()
        : this(OrganizationErrorCodes.Unavailable, "The organization is unavailable.")
    {
    }



    /// <summary>
    /// Creates the exception with a message and no specific code.
    /// </summary>
    public OrganizationException(string message)
        : this(OrganizationErrorCodes.Unavailable, message)
    {
    }



    /// <summary>
    /// Creates the exception with a message, a cause and no specific code.
    /// </summary>
    public OrganizationException(string message, Exception innerException)
        : this(OrganizationErrorCodes.Unavailable, message, innerException)
    {
    }



    /// <summary>
    /// The error code the API answers with.
    /// </summary>
    public ErrorCode Code { get; }
}
