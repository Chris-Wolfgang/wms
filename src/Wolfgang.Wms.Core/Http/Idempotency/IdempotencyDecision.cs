// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Http.Idempotency;

/// <summary>
/// What an endpoint does with a request that carries an <c>Idempotency-Key</c> (E82.3).
/// </summary>
public enum IdempotencyDecision
{
    /// <summary>
    /// No record for this caller and key: run the handler and store its response.
    /// </summary>
    Proceed = 0,

    /// <summary>
    /// Same key and same body as a stored request: send the stored response again, run nothing.
    /// </summary>
    Replay = 1,

    /// <summary>
    /// Same key but a different body: answer 422, run nothing.
    /// </summary>
    Conflict = 2,
}
