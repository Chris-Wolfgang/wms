// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Web;
using Wolfgang.Wms.Web.Components;
using Wolfgang.Wms.Web.Shared;

var builder = WebApplication.CreateBuilder(args);

// Blazor Web App in Server render mode for v1 (E82.4); components are render-mode-agnostic.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Workspace entry gate: free tier until the license (E79) and identity (E11) endpoints replace it.
builder.Services.AddSingleton<IWorkspaceAccess, FreeTierWorkspaceAccess>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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

/// <summary>
/// Kept internal on purpose: tests host the console through <c>WebApplicationFactory&lt;App&gt;</c> (any public
/// type of this assembly), so the entry point never collides with the API host's public <c>Program</c>.
/// </summary>
internal sealed partial class Program
{
}
