// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Copies;

/// <summary>
/// What an administrator sends to copy a site (E16.5): the fields that must differ and a checklist of what
/// to bring. Transactional data and pickers' assignments are never copied.
/// </summary>
/// <param name="Code">The new site's code, unique across sites.</param>
/// <param name="Name">The new site's name.</param>
/// <param name="TimeZone">The new site's time zone; null to keep the source site's.</param>
/// <param name="Settings">Bring the source site's settings overrides (configured values and cascade modes; secrets are never copied).</param>
/// <param name="Zones">Bring the zones with their reject-lane flags, resolution properties and settings overrides.</param>
/// <param name="Locations">Bring the locations (requires <paramref name="Zones"/>), with the code substitution applied to codes and barcodes.</param>
/// <param name="CodePrefixFrom">Optional: the prefix of the source codes and barcodes to replace (a renumbering such as aisle <c>A</c> to <c>B</c>).</param>
/// <param name="CodePrefixTo">The replacement prefix; required when <paramref name="CodePrefixFrom"/> is given.</param>
public sealed record SiteCopyRequest
(
    string Code,
    string Name,
    string? TimeZone = null,
    bool Settings = true,
    bool Zones = true,
    bool Locations = true,
    string? CodePrefixFrom = null,
    string? CodePrefixTo = null
);
