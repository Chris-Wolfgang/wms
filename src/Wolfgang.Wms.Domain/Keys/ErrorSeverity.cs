// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// How an <see cref="ErrorCode"/> is surfaced: whether the operation stopped, and how loudly.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>The operation completed; the code carries information the caller may act on.</summary>
    Info = 0,

    /// <summary>The operation completed with a condition the caller should review.</summary>
    Warning = 1,

    /// <summary>The operation did not complete.</summary>
    Error = 2,
}
