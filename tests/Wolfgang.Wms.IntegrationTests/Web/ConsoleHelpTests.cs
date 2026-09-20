// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Web;
using Wolfgang.Wms.Web.Shared;

namespace Wolfgang.Wms.IntegrationTests.Web;

/// <summary>
/// E83.4: with the packaged site next to the console, <c>/help/</c> serves it (and <c>/help</c> lands on
/// its index); without it, <c>/help/…</c> is a 404 that names the online site. Help links carry the article
/// and, for an error, the code's anchor.
/// </summary>
public sealed class ConsoleHelpTests
{
    [Fact]
    public async Task The_packaged_site_is_served_under_help()
    {
        var root = Path.Combine(Path.GetTempPath(), "wms-help-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, ConsoleHelp.Folder, "docs"));
        await File.WriteAllTextAsync(Path.Combine(root, ConsoleHelp.Folder, "index.html"), "<h1>Manual</h1>");
        await File.WriteAllTextAsync(Path.Combine(root, ConsoleHelp.Folder, "docs", "troubleshooting.html"), "<h1>Troubleshooting</h1>");
        try
        {
            await using var app = await StartAsync(root);
            using var client = app.GetTestClient();

            using var index = await client.GetAsync(new Uri("/help/index.html", UriKind.Relative));
            using var article = await client.GetAsync(new Uri(HelpLinks.Troubleshooting(AuthErrorCodes.Forbidden), UriKind.Relative));
            using var bare = await client.GetAsync(new Uri("/help", UriKind.Relative));

            Assert.Equal(HttpStatusCode.OK, index.StatusCode);
            Assert.Equal("<h1>Manual</h1>", await index.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, article.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, bare.StatusCode);
            Assert.Equal("/help/index.html", bare.Headers.Location?.ToString());
            await app.StopAsync();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }



    [Fact]
    public async Task Without_the_site_help_is_a_404_that_names_the_online_manual()
    {
        var root = Path.Combine(Path.GetTempPath(), "wms-nohelp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var app = await StartAsync(root);
            using var client = app.GetTestClient();

            using var response = await client.GetAsync(new Uri("/help/docs/troubleshooting.html", UriKind.Relative));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Contains(HelpLinks.OnlineRoot, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            Assert.Throws<ArgumentNullException>(() => ConsoleHelp.MapConsoleHelp(null!));
            await app.StopAsync();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }



    [Fact]
    public void Links_carry_the_article_the_anchor_and_the_version()
    {
        Assert.Equal("/help/docs/troubleshooting.html#auth-forbidden", HelpLinks.Troubleshooting(AuthErrorCodes.Forbidden));
        Assert.Equal("/help/docs/getting-started.html", HelpLinks.Local("/docs/getting-started/"));
        Assert.Equal("https://chris-wolfgang.github.io/wms/versions/v0.1.0/docs/licensing.html", HelpLinks.Online("v0.1.0", "docs/licensing"));
        Assert.Throws<ArgumentException>(() => HelpLinks.Local(" "));
        Assert.Throws<ArgumentNullException>(() => HelpLinks.Troubleshooting(null!));
        Assert.Throws<ArgumentException>(() => HelpLinks.Online(" ", "a"));
        Assert.Throws<ArgumentException>(() => HelpLinks.Online("v1", " "));
    }



    private static async Task<WebApplication> StartAsync(string contentRoot)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = contentRoot });
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.MapConsoleHelp();
        await app.StartAsync();
        return app;
    }
}
