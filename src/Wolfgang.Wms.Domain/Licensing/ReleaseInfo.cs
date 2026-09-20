// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// What licensing knows about this release (E79.1, E79.5): the version its tier table belongs to and the
/// date a key's coverage is checked against. Both are constants the release skill updates when a release is
/// cut (the build must stay deterministic, so no build-time clock), and a test keeps the date from drifting
/// into the future.
/// </summary>
public static class ReleaseInfo
{
    /// <summary>
    /// The release whose tier table this is (the next release when unreleased).
    /// </summary>
    public const string Version = "0.1.0";



    /// <summary>
    /// The release date a paid key's coverage must contain for this build to be installable (E79.5).
    /// </summary>
    public static DateOnly ReleaseDate { get; } = new(2026, 9, 20);
}
