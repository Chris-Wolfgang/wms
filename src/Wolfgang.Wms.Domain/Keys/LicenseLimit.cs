// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// A numeric limit carried by a license key, for example <c>devices</c> or <c>sites</c>, checked with
/// <c>ILicense.Check(LicenseLimits.Devices, n)</c> (E79.4).
/// </summary>
/// <param name="Name">Stable limit name.</param>
/// <param name="Description">One-line, user-facing description.</param>
public sealed record LicenseLimit(string Name, string Description)
{
    /// <summary>
    /// Stable limit name.
    /// </summary>
    public string Name { get; } = KeyName.Require(Name, nameof(Name));



    /// <summary>
    /// One-line, user-facing description.
    /// </summary>
    public string Description { get; } = RequireDescription(Description);



    private static string RequireDescription(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return description;
    }
}
