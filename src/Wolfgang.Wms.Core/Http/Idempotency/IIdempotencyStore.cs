// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Http.Idempotency;

/// <summary>
/// Storage for <see cref="IdempotencyRecord"/>s (E82.3), keyed by caller and key. Infrastructure implements
/// it on the database so the check and the handler's writes share one transaction (ADR 0002); expired records
/// are purged by a job.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// The stored record for this caller and key, or null when none exists or it has expired.
    /// </summary>
    Task<IdempotencyRecord?> FindAsync(string caller, IdempotencyKey key, CancellationToken cancellationToken);



    /// <summary>
    /// Stores the record for the completed request. Storing a second record for the same caller and key is an
    /// error; callers check with <see cref="FindAsync"/> first, inside the same transaction.
    /// </summary>
    Task SaveAsync(IdempotencyRecord record, CancellationToken cancellationToken);
}
