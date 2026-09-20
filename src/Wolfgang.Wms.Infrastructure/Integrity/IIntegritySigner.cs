// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Integrity;

/// <summary>
/// Signs and verifies security-critical rows (E10.4) with an HMAC keyed by a per-installation secret that
/// only the application can read (it is protected by the Data Protection ring).
/// </summary>
public interface IIntegritySigner
{
    /// <summary>
    /// The signature of <paramref name="content"/>.
    /// </summary>
    Task<string> SignAsync(string content, CancellationToken cancellationToken);



    /// <summary>
    /// True when the row's signature matches its content. A failure is logged at Error and counted; the
    /// caller does not honour the row.
    /// </summary>
    Task<bool> IsValidAsync(ISignedEntity entity, string table, CancellationToken cancellationToken);



    /// <summary>
    /// Signs every added or changed <see cref="ISignedEntity"/> in <paramref name="entities"/> in place.
    /// </summary>
    Task SignAllAsync(IEnumerable<ISignedEntity> entities, CancellationToken cancellationToken);
}
