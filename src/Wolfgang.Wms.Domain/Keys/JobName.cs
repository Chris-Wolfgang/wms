// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// The stable name of a worker job, for example <c>core.outbox_sender</c>, used for run history, the
/// "run now" action and enable/disable settings (E1.10, E12). Never a free string.
/// </summary>
/// <param name="Name">Stable job name.</param>
/// <param name="Description">One-line, user-facing description shown on the jobs page.</param>
public sealed record JobName(string Name, string Description)
{
    /// <summary>
    /// Stable job name.
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
