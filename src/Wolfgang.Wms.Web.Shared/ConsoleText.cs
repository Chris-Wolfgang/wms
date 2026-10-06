// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Web.Shared;

/// <summary>
/// The console's text (E82.4): every user-visible string of the host and the workspaces lives in
/// <c>Resources/ConsoleText.resx</c> and is read through <c>IStringLocalizer&lt;ConsoleText&gt;</c>, never written
/// as a literal in a component (<c>ConsoleMarkupTests</c> enforces it). This type names the resource and is never
/// instantiated.
/// </summary>
public abstract class ConsoleText
{
    /// <summary>
    /// The resource file this type names, relative to the <c>Wolfgang.Wms.Web.Shared</c> project: the
    /// localizer finds it from the type's name and <see cref="Domain.Localization.Cultures.ResourcesPath"/>.
    /// </summary>
    public const string ResourceFile = "Resources/ConsoleText.resx";
}
