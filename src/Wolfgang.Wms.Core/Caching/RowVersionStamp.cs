// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Runtime.InteropServices;

namespace Wolfgang.Wms.Core.Caching;

/// <summary>
/// The change stamp of a set of tables (E1.12, ADR 0003): the highest <c>row_version</c> among their rows
/// and how many rows they hold. An insert or update moves the version; a delete moves the count; either
/// makes the stamp differ, so a cache or an entity tag built from it sees every kind of change.
/// </summary>
/// <param name="MaxRowVersion">The highest row version across the tables, or 0 when they are all empty.</param>
/// <param name="RowCount">The total number of rows across the tables.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct RowVersionStamp(ulong MaxRowVersion, long RowCount);
