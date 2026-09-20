// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Licensing;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The effective license of this install (E79.1–E79.5): what a feature edge asks (<see cref="HasFeature"/>)
/// and what a creation checks (<see cref="Limit"/>, through <see cref="LicenseGate"/>). Recomputed from the
/// installed keys on install and on a cadence, so every instance follows within seconds and no restart is
/// needed.
/// </summary>
public interface ILicense
{
    /// <summary>
    /// The license in force.
    /// </summary>
    EffectiveLicense Current { get; }



    /// <summary>
    /// True when the feature is granted; false is the answer for anything the tier table and the keys do not name.
    /// </summary>
    bool HasFeature(LicenseFeature feature);



    /// <summary>
    /// The licensed value of a limit.
    /// </summary>
    LimitValue Limit(LicenseLimit limit);



    /// <summary>
    /// Re-reads the installed keys and recomputes the license now.
    /// </summary>
    Task<EffectiveLicense> RefreshAsync(CancellationToken cancellationToken);
}
