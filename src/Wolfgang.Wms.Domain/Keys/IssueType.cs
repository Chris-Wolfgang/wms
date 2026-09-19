// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// A kind of operational issue the system raises for a person to resolve, for example
/// <c>picking.short_pick</c> or <c>jobs.repeated_failure</c>. Modules define theirs in a definitions class.
/// </summary>
/// <param name="Name">Stable issue type name.</param>
/// <param name="Description">One-line, user-facing description.</param>
public sealed record IssueType(string Name, string Description)
{
    /// <summary>
    /// Stable issue type name.
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
