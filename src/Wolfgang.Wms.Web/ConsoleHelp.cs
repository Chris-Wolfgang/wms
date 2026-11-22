// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.FileProviders;
using Wolfgang.Wms.Web.Shared;

namespace Wolfgang.Wms.Web;

/// <summary>
/// The console's local help (E83.4): the release packages the built docs site next to the console under
/// <c>help/</c>, and the console serves it at <c>/help/</c>, so every Help link opens the manual of the
/// installed version, on air-gapped sites too. Without the folder, <c>/help/…</c> answers 404 with a plain
/// pointer to the online site.
/// </summary>
public static class ConsoleHelp
{
    /// <summary>
    /// The folder next to the console that holds the packaged site.
    /// </summary>
    public const string Folder = "help";



    /// <summary>
    /// Serves the packaged site at <c>/help/</c> when the folder exists.
    /// </summary>
    /// <returns>True when the site is packaged with this install.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static bool MapConsoleHelp(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var root = Path.Combine(app.Environment.ContentRootPath, Folder);
        if (Directory.Exists(root))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                RequestPath = HelpLinks.Root,
                FileProvider = new PhysicalFileProvider(root),
            });
            app.MapGet(HelpLinks.Root, () => Results.Redirect(HelpLinks.Root + "/index.html")).ExcludeFromDescription();
            return true;
        }

        app.MapGet(HelpLinks.Root + "/{**path}", () => Results.Text("The help site is not packaged with this install. The online manual is at " + HelpLinks.OnlineRoot + ".", statusCode: StatusCodes.Status404NotFound)).ExcludeFromDescription();
        return false;
    }
}
