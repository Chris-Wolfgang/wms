// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.UnitTests.Authorization;

/// <summary>
/// E10.1 without a host: grant formats and matching per organisation and site, the site of a request, the
/// catalog from modules and the console, the on-demand policies, the handler, and the endpoint declaration.
/// </summary>
public sealed class PermissionTests
{
    private static readonly Permission Write = new("settings.write", "Change settings");
    private static readonly Permission Read = new("settings.read", "View settings");



    [Fact]
    public void Grants_match_everywhere_or_at_the_named_site_only()
    {
        var organisation = Principal(PermissionClaims.OrganizationGrant("settings.write"));
        var site3 = Principal(PermissionClaims.SiteGrant("settings.write", 3));
        var wildcardSite = Principal(PermissionClaims.SiteGrant("*", 3));
        var everything = Principal(PermissionClaims.OrganizationGrant("*"));

        Assert.True(PermissionClaims.Allows(organisation, Write, siteId: null));
        Assert.True(PermissionClaims.Allows(organisation, Write, 9));
        Assert.False(PermissionClaims.Allows(organisation, Read, 9));
        Assert.False(PermissionClaims.Allows(site3, Write, siteId: null));
        Assert.True(PermissionClaims.Allows(site3, Write, 3));
        Assert.False(PermissionClaims.Allows(site3, Write, 4));
        Assert.True(PermissionClaims.Allows(wildcardSite, Read, 3));
        Assert.False(PermissionClaims.Allows(wildcardSite, Read, siteId: null));
        Assert.True(PermissionClaims.Allows(everything, Read, 4));
        Assert.False(PermissionClaims.Allows(null, Read, 4));
        Assert.Equal(["settings.write@site:3"], PermissionClaims.GrantsOf(site3));
        Assert.Empty(PermissionClaims.GrantsOf(null));
        Assert.Throws<ArgumentException>(() => PermissionClaims.OrganizationGrant(" "));
        Assert.Throws<ArgumentException>(() => PermissionClaims.SiteGrant("", 1));
        Assert.Throws<ArgumentNullException>(() => PermissionClaims.Allows(organisation, null!, null));
    }



    [Fact]
    public void The_site_of_a_request_comes_from_the_route_then_the_header()
    {
        var byHeader = new DefaultHttpContext();
        byHeader.Request.Headers[SiteContext.Header] = "7";
        var byRoute = new DefaultHttpContext();
        byRoute.Request.RouteValues[SiteContext.RouteValue] = "5";
        byRoute.Request.Headers[SiteContext.Header] = "7";
        var badHeader = new DefaultHttpContext();
        badHeader.Request.Headers[SiteContext.Header] = "seven";

        Assert.Equal(7, SiteContext.SiteIdOf(byHeader));
        Assert.Equal(5, SiteContext.SiteIdOf(byRoute));
        Assert.Null(SiteContext.SiteIdOf(badHeader));
        Assert.Null(SiteContext.SiteIdOf(new DefaultHttpContext()));
        Assert.Throws<ArgumentNullException>(() => SiteContext.SiteIdOf(null!));
    }



    [Fact]
    public async Task Catalog_policies_and_handler_work_together()
    {
        var modules = new ModuleCollection();
        modules.Add(ModuleDescriptor.Create("settings").WithPermissions(Read, Write));
        var catalog = new PermissionCatalog(modules);
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()), catalog);
        var handler = new PermissionAuthorizationHandler();

        var policy = await provider.GetPolicyAsync(PermissionPolicyProvider.PolicyName("settings.write"));
        var unknown = await provider.GetPolicyAsync("permission:nope");
        var other = await provider.GetPolicyAsync("some-other-policy");
        var context = new AuthorizationHandlerContext([new PermissionRequirement(Write)], Principal(PermissionClaims.SiteGrant("settings.write", 3)), resource: SiteRequest(3));
        var elsewhere = new AuthorizationHandlerContext([new PermissionRequirement(Write)], Principal(PermissionClaims.SiteGrant("settings.write", 3)), resource: SiteRequest(4));
        await handler.HandleAsync(context);
        await handler.HandleAsync(elsewhere);

        Assert.Equal(new[] { "settings.read", "settings.write" }.Concat(ConsolePermissions.All.Select(p => p.Name)).Order(StringComparer.Ordinal), catalog.All.Select(p => p.Name));
        Assert.Equal("settings", catalog.All.Single(p => string.Equals(p.Name, "settings.write", StringComparison.Ordinal)).Module);
        Assert.Same(Write, catalog.Find("settings.write"));
        Assert.Null(catalog.Find("nope"));
        Assert.Null(catalog.Find(null));
        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, r => r is PermissionRequirement { Permission.Name: "settings.write" });
        Assert.Null(unknown);
        Assert.Null(other);
        Assert.NotNull(await provider.GetDefaultPolicyAsync());
        Assert.Null(await provider.GetFallbackPolicyAsync());
        Assert.True(context.HasSucceeded);
        Assert.False(elsewhere.HasSucceeded);
        Assert.Throws<InvalidOperationException>(() => new PermissionCatalog(Modules(ModuleDescriptor.Create("a").WithPermissions(Write), ModuleDescriptor.Create("b").WithPermissions(new Permission("settings.write", "again")))));
        Assert.Throws<InvalidOperationException>(() => new PermissionCatalog(Modules(ModuleDescriptor.Create("a").WithPermissions(ConsolePermissions.EnterConfigure))));
        Assert.Throws<ArgumentNullException>(() => new PermissionCatalog(null!));
        Assert.Throws<ArgumentNullException>(() => new PermissionPolicyProvider(null!, catalog));
        Assert.Throws<ArgumentNullException>(() => new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()), null!));
        Assert.Throws<ArgumentNullException>(() => new PermissionRequirement(null!));
        Assert.Throws<ArgumentNullException>(() => new PermissionMetadata(null!));
        Assert.Throws<ArgumentException>(() => PermissionPolicyProvider.PolicyName(""));
        await Assert.ThrowsAsync<ArgumentNullException>(() => provider.GetPolicyAsync(null!));
    }



    [Fact]
    public void Endpoints_declare_their_permission_as_metadata_and_a_policy()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();

        var endpoint = app.MapGet("/x", () => "x").RequirePermission(Write);
        var metadata = ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints).Single().Metadata;

        Assert.NotNull(endpoint);
        Assert.Same(Write, metadata.GetMetadata<PermissionMetadata>()!.Permission);
        Assert.Equal("permission:settings.write", metadata.GetMetadata<IAuthorizeData>()!.Policy);
        Assert.Throws<ArgumentNullException>(() => app.MapGet("/y", () => "y").RequirePermission(null!));
        Assert.Throws<ArgumentNullException>(() => PermissionEndpointExtensions.RequirePermission<RouteHandlerBuilder>(null!, Write));
        Assert.Throws<ArgumentNullException>(() => PermissionEndpointExtensions.AddWmsPermissions(null!));
        Assert.Same(ConsolePermissions.EnterConfigure, ConsolePermissions.For("configure", "Configure"));
        Assert.Equal("workspace.new.enter", ConsolePermissions.For("new", "New").Name);
        Assert.Throws<ArgumentException>(() => ConsolePermissions.For(" ", "x"));
    }



    private static ClaimsPrincipal Principal(params string[] grants)
    {
        var identity = new ClaimsIdentity("test");
        foreach (var grant in grants)
        {
            identity.AddClaim(new Claim(PermissionClaims.ClaimType, grant));
        }

        return new ClaimsPrincipal(identity);
    }



    private static HttpContext SiteRequest(long siteId)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[SiteContext.Header] = siteId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return context;
    }



    private static ModuleCollection Modules(params ModuleDescriptor[] descriptors)
    {
        var modules = new ModuleCollection();
        foreach (var descriptor in descriptors)
        {
            modules.Add(descriptor);
        }

        return modules;
    }
}
