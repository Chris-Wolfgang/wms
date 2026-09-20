// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Settings;

/// <summary>
/// How a setting's value is edited and stored (E6.1). Every value is stored as invariant-culture text; the
/// kind tells the settings page which editor to show and the accessor how to parse the text.
/// </summary>
public enum SettingKind
{
    /// <summary>A <c>true</c>/<c>false</c> switch.</summary>
    Boolean,

    /// <summary>A whole number (<c>int</c> or <c>long</c>).</summary>
    Integer,

    /// <summary>A decimal number, stored with a <c>.</c> decimal point.</summary>
    Number,

    /// <summary>One of a fixed list of names (a C# enum), stored by name.</summary>
    Enum,

    /// <summary>Free text.</summary>
    String,

    /// <summary>A duration (<see cref="TimeSpan"/>), stored as <c>[d.]hh:mm:ss[.fffffff]</c>.</summary>
    Duration,

    /// <summary>A point in time (<see cref="DateTimeOffset"/>), stored as ISO 8601 with offset.</summary>
    Timestamp,

    /// <summary>A credential (<see cref="SecretText"/>): encrypted at rest, masked on screen (E8.3).</summary>
    Secret,

    /// <summary>A structured value stored as JSON through a codec the key's author supplies.</summary>
    Json,
}
