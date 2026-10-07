// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Copies;

/// <summary>
/// What an administrator sends to copy a zone within or across sites (E16.5).
/// </summary>
/// <param name="Code">The new zone's code, unique within the target site.</param>
/// <param name="Name">The new zone's name.</param>
/// <param name="TargetSiteId">The site to copy into; null for the source zone's own site.</param>
/// <param name="Settings">Bring the zone's settings overrides (secrets are never copied).</param>
/// <param name="Locations">Bring the zone's locations, with the code substitution applied to codes and barcodes.</param>
/// <param name="CodePrefixFrom">Optional: the prefix of the source location codes and barcodes to replace; required when copying locations within the same site.</param>
/// <param name="CodePrefixTo">The replacement prefix; required when <paramref name="CodePrefixFrom"/> is given.</param>
public sealed record ZoneCopyRequest
(
    string Code,
    string Name,
    long? TargetSiteId = null,
    bool Settings = true,
    bool Locations = true,
    string? CodePrefixFrom = null,
    string? CodePrefixTo = null
);
