// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Http.Paging;

namespace Wolfgang.Wms.Core.Locations;

/// <summary>
/// One page of a site's locations (E17.1): the optional zone filter, the id window parallel clients split a
/// table by, and the resolved keyset page.
/// </summary>
/// <param name="ZoneId">Only the locations of this zone; null for the whole site.</param>
/// <param name="IdFrom">Only ids at or above this; null for no lower bound.</param>
/// <param name="IdTo">Only ids at or below this; null for no upper bound.</param>
/// <param name="Page">The sort, cursor, direction and size, resolved from the request by <see cref="PageRequest.TryResolve"/>.</param>
public sealed record LocationQuery
(
    long? ZoneId,
    long? IdFrom,
    long? IdTo,
    PageQuery Page
)
{
    /// <summary>The page, never null.</summary>
    public PageQuery Page { get; } = Page ?? throw new ArgumentNullException(nameof(Page));
}
