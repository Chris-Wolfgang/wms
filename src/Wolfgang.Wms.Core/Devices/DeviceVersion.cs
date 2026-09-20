// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Devices;

/// <summary>
/// The app version a handheld sends on every call (E82.7) and how it is compared with the minimum the server
/// requires. The header carries the app's <c>versionName</c> (<c>0.4.2</c> or a MinVer height such as
/// <c>0.4.3-alpha.0.7</c>); only the numeric part counts.
/// </summary>
public static class DeviceVersion
{
    /// <summary>
    /// Request header carrying the device app version.
    /// </summary>
    public const string HeaderName = "X-Wms-Device-Version";



    /// <summary>
    /// Parses a header value: the dotted numeric prefix (two to four parts) before any <c>-</c> or <c>+</c>
    /// suffix. False for anything else.
    /// </summary>
    public static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.AsSpan().Trim();
        var cut = span.IndexOfAny('-', '+');
        if (cut >= 0)
        {
            span = span[..cut];
        }

        return Version.TryParse(span, out var parsed) && (version = parsed) is not null;
    }



    /// <summary>
    /// True when <paramref name="version"/> is at least <paramref name="minimum"/>, comparing major, minor
    /// and build; a missing build or revision counts as zero on either side.
    /// </summary>
    public static bool Satisfies(Version version, Version minimum)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(minimum);

        return Normalize(version) >= Normalize(minimum);
    }



    private static Version Normalize(Version version)
    {
        return new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
    }
}
