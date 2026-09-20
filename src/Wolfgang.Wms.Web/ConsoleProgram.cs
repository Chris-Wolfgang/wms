// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.HttpOverrides;
using Wolfgang.Wms.Web.Components;
using Wolfgang.Wms.Web.Shared;

using Wolfgang.Wms.Logging;

namespace Wolfgang.Wms.Web;

/// <summary>
/// Entry point of the console host. An explicit class rather than top-level statements so the type is not
/// named <c>Program</c> like the API host's: coverage tooling and shared test assemblies keep the two apart.
/// Tests host the console through <c>WebApplicationFactory&lt;App&gt;</c>.
/// </summary>
public static class ConsoleProgram
{
    /// <summary>
    /// Builds and runs the console host.
    /// </summary>
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Host.UseWindowsService(options => options.ServiceName = "WolfgangWms.Web");   // E15.1: a no-op outside a service
        builder.UseWmsSerilog();   // E12.2: the same log pipeline as the API; the console's level follows Wms:Logging only (its settings ride the API)

        // Blazor Web App in Server render mode for v1 (E82.4); components are render-mode-agnostic.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Workspace entry gate: free tier until the license (E79) and identity (E11) endpoints replace it.
        builder.Services.AddSingleton<IWorkspaceAccess, FreeTierWorkspaceAccess>();

        // E10.6: the same Wms:Hosting:BehindProxy key as the API (the console cannot share Core's code).
        builder.Services.Configure<ForwardedHeadersOptions>(options => ConsoleHosting.ConfigureForwardedHeaders(options, builder.Configuration));

        var app = builder.Build();

        app.UseForwardedHeaders();
        app.UseWmsSecurityHeaders();   // E10.6: CSP for Blazor Server, nosniff, referrer policy, no framing

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days; revisit for production (https://aka.ms/aspnetcore-hsts).
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies([.. WorkspaceAssemblies.All]);

        app.Run();
    }
}
