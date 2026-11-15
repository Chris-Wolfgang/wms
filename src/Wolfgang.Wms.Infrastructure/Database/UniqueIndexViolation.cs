// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;

namespace Wolfgang.Wms.Infrastructure.Database;

/// <summary>
/// Recognises a save refused by a named unique index, on either provider. An availability check before a save
/// cannot close the race between two writers, so the index is the final guarantee and the store turns its refusal
/// into the same conflict the check reports. Both providers name the index in the error text.
/// </summary>
public static class UniqueIndexViolation
{
    /// <summary>
    /// True when <paramref name="exception"/> was caused by <paramref name="indexName"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static bool Is(DbUpdateException exception, string indexName)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(indexName);

        return exception.InnerException?.Message.Contains(indexName, StringComparison.Ordinal) == true;
    }
}
