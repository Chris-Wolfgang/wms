// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Database.Sync;

/// <summary>
/// A master table devices synchronise (E5.3, E5.4): barcodes, SKUs, locations, paths, settings, messages.
/// Versioned (the delta watermark), soft-deleted (deletions appear in deltas) and identified by the
/// server-assigned <c>id</c> (the manifest key). One helper generates <c>?since=</c> and <c>/manifest</c>
/// for every such table (<see cref="SyncEndpoints"/>).
/// </summary>
public interface ISyncedEntity : IVersionedEntity, ISoftDeletable
{
    /// <summary>
    /// The server-assigned identifier.
    /// </summary>
    long Id { get; }
}
