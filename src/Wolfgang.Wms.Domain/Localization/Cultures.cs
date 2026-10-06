// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Localization;

/// <summary>
/// The cultures the product ships strings for (E1.14): the one list every host reads, the API through
/// <c>WmsLocalization</c> in Core and the console through its own registration, since the console may not
/// reference Core (E82.1). English is the only shipped language in v1; adding a culture is one entry here plus
/// the resource files.
/// </summary>
public static class Cultures
{
    /// <summary>
    /// The culture used when the request names none of the supported ones.
    /// </summary>
    public const string Default = "en";

    /// <summary>
    /// Folder, relative to each project, that holds the <c>.resx</c> files.
    /// </summary>
    public const string ResourcesPath = "Resources";



    /// <summary>
    /// Every supported culture, in preference order for fallback.
    /// </summary>
    public static IReadOnlyList<string> Supported { get; } = [Default];
}
