// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// A license tier (E79.1): a name that is never renamed or removed once shipped (only added), and a rank so
/// "moved to a higher tier" is a comparison. What a tier grants in a release is the release's
/// <see cref="TierTable"/>, never the tier itself.
/// </summary>
/// <param name="Name">The stable name carried in keys (<c>free</c>, <c>pro</c>, <c>enterprise</c>).</param>
/// <param name="Rank">The order: a higher rank is a higher tier.</param>
public sealed record LicenseTier(string Name, int Rank)
{
    /// <summary>
    /// The stable name.
    /// </summary>
    public string Name { get; } = KeyName.Require(Name, nameof(Name));
}
