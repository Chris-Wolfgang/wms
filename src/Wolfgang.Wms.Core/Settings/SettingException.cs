// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Settings;

/// <summary>
/// A settings read or write the registry or the store refuses (E6.3), carrying the error code the API
/// answers with (<see cref="SettingExceptionHandler"/>).
/// </summary>
[SuppressMessage("Roslynator", "RCS1194:Implement exception constructors", Justification = "Every SettingException carries the error code the API answers with; the standard constructors would create one without it.")]
public sealed class SettingException : Exception
{
    /// <summary>
    /// Creates the exception for <paramref name="code"/> with a user-facing detail and the values for the code's
    /// message template, which the problem response's title is formatted from.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> or <paramref name="arguments"/> is null.</exception>
    public SettingException(ErrorCode code, string detail, params object[] arguments)
        : base(detail)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
    }



    /// <summary>
    /// Creates the exception for <paramref name="code"/> with a user-facing detail, a cause and the values for the
    /// code's message template.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> or <paramref name="arguments"/> is null.</exception>
    public SettingException(ErrorCode code, string detail, Exception innerException, params object[] arguments)
        : base(detail, innerException)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
    }



    /// <summary>
    /// The error code the API answers with.
    /// </summary>
    public ErrorCode Code { get; }



    /// <summary>
    /// The values for <see cref="Code"/>'s message template, in placeholder order (<c>{0}</c>, <c>{1}</c>…).
    /// </summary>
    public IReadOnlyList<object> Arguments { get; }
}
