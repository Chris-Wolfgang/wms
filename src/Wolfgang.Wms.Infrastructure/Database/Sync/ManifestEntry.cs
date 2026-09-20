// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Database.Sync;

/// <summary>
/// One row of a table manifest (E5.4): the id and the row version of every live row, from an index-only
/// scan. A device compares it with its local copy, fetches missing or newer ids in batches, and deletes local
/// rows absent from the manifest.
/// </summary>
/// <param name="Id">The server-assigned identifier.</param>
/// <param name="RowVersion">The row's current version.</param>
public readonly record struct ManifestEntry(long Id, long RowVersion);
