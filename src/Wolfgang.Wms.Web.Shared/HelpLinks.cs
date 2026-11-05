// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Web.Shared;

/// <summary>
/// Where Help links point (E83.4): the site packaged with the console under <c>/help/</c> (the installed
/// version by construction), and the public site for a "view online" link when it is reachable.
/// </summary>
public static class HelpLinks
{
    /// <summary>The console path the packaged site is served under.</summary>
    public const string Root = "/help";

    /// <summary>The public site's root (the `latest` alias).</summary>
    public const string OnlineRoot = "https://chris-wolfgang.github.io/wms/versions/latest/";



    /// <summary>
    /// The local page for a docs article, for example <c>docs/troubleshooting</c>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="article"/> is blank.</exception>
    public static string Local(string article)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(article);
        return $"{Root}/{article.Trim('/')}.html";
    }



    /// <summary>
    /// The troubleshooting entry for an error code, on the local site.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public static string Troubleshooting(ErrorCode code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return $"{Local("docs/troubleshooting")}#{code.DocsAnchor}";
    }



    /// <summary>
    /// The public page for the same article under a version, for the "view online" link.
    /// </summary>
    /// <exception cref="ArgumentException">An argument is blank.</exception>
    public static string Online(string version, string article)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(article);
        return $"https://chris-wolfgang.github.io/wms/versions/{version.Trim('/')}/{article.Trim('/')}.html";
    }
}
