// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Http.Paging;

/// <summary>
/// Which way a keyset page is read from its cursor.
/// </summary>
public enum PageDirection
{
    /// <summary>
    /// Rows after the cursor in sort order (or the first page when there is no cursor).
    /// </summary>
    Forward = 0,

    /// <summary>
    /// Rows before the cursor in sort order.
    /// </summary>
    Backward = 1,
}
