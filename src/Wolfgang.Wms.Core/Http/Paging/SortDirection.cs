// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Http.Paging;

/// <summary>
/// The order a list is sorted in; independent of <see cref="PageDirection"/>, which is the way a page is read
/// from its cursor.
/// </summary>
public enum SortDirection
{
    /// <summary>
    /// Smallest value first.
    /// </summary>
    Ascending = 0,

    /// <summary>
    /// Largest value first.
    /// </summary>
    Descending = 1,
}
