// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Sites;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.UnitTests.Authorization;

public sealed class SiteScopeTests
{
    [Fact]
    public void An_organization_grant_of_the_permission_or_the_wildcard_is_unrestricted()
    {
        Assert.True(SiteScope.Of(Principal("sites.read@organization"), SitesModule.Read).IsUnrestricted);
        Assert.True(SiteScope.Of(Principal("*@organization", "sites.read@site:3"), SitesModule.Read).IsUnrestricted);
        Assert.True(SiteScope.Everywhere.Contains(123));
        Assert.Empty(SiteScope.Everywhere.SiteIds);
    }



    [Fact]
    public void Site_grants_of_the_permission_or_the_wildcard_name_the_sites_and_other_permissions_do_not_count()
    {
        var scope = SiteScope.Of(Principal("sites.read@site:3", "*@site:7", "sites.write@site:9", "zones.read@organization", "sites.read@site:x", "sites.read@site:3"), SitesModule.Read);

        Assert.False(scope.IsUnrestricted);
        Assert.Equal([3L, 7L], scope.SiteIds);
        Assert.True(scope.Contains(3));
        Assert.True(scope.Contains(7));
        Assert.False(scope.Contains(9));
    }



    [Fact]
    public void No_principal_no_grants_and_explicit_sets_behave_as_declared()
    {
        var none = SiteScope.Of(null, SitesModule.Read);
        var only = SiteScope.Only([5, 2, 5]);

        Assert.Equal(SiteScope.Nowhere.SiteIds, none.SiteIds);
        Assert.False(none.IsUnrestricted);
        Assert.False(none.Contains(1));
        Assert.Empty(SiteScope.Of(Principal(), SitesModule.Read).SiteIds);
        Assert.Equal([2L, 5L], only.SiteIds);
        Assert.Throws<ArgumentNullException>(() => SiteScope.Of(Principal(), null!));
        Assert.Throws<ArgumentNullException>(() => SiteScope.Only(null!));
    }



    [Fact]
    public void The_query_filter_leaves_an_unrestricted_scope_alone_and_checks_its_arguments()
    {
        var rows = new[] { new Row(1, 10), new Row(2, 20) }.AsQueryable();

        Assert.Same(rows, rows.InScope(SiteScope.Everywhere));
        Assert.Throws<ArgumentNullException>(() => SiteScopeQueries.InScope<Row>(null!, SiteScope.Everywhere));
        Assert.Throws<ArgumentNullException>(() => rows.InScope(null!));
        Assert.Throws<ArgumentException>(() => rows.InScope(SiteScope.Nowhere, " "));
    }



    [Fact]
    public async Task A_collection_endpoint_admits_a_site_level_grant_and_a_routed_or_unmarked_endpoint_does_not()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(SitesModule.Read);
        var siteReader = Principal("sites.read@site:3");

        var marked = await DecideAsync(handler, requirement, siteReader, marked: true, routeSiteId: null);
        var markedWrongPermission = await DecideAsync(handler, requirement, Principal("zones.read@site:3"), marked: true, routeSiteId: null);
        var unmarked = await DecideAsync(handler, requirement, siteReader, marked: false, routeSiteId: null);
        var routedOwnSite = await DecideAsync(handler, requirement, siteReader, marked: true, routeSiteId: 3);
        var routedOtherSite = await DecideAsync(handler, requirement, siteReader, marked: true, routeSiteId: 4);
        var organization = await DecideAsync(handler, requirement, Principal("sites.read@organization"), marked: false, routeSiteId: null);

        Assert.True(marked);
        Assert.False(markedWrongPermission);
        Assert.False(unmarked);
        Assert.True(routedOwnSite);
        Assert.False(routedOtherSite);
        Assert.True(organization);
        Assert.Throws<ArgumentNullException>(() => new CollectionScopeMetadata(null!));
    }



    private static async Task<bool> DecideAsync(PermissionAuthorizationHandler handler, PermissionRequirement requirement, ClaimsPrincipal principal, bool marked, long? routeSiteId)
    {
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var metadata = marked ? new Microsoft.AspNetCore.Http.EndpointMetadataCollection(new CollectionScopeMetadata(requirement.Permission)) : Microsoft.AspNetCore.Http.EndpointMetadataCollection.Empty;
        http.SetEndpoint(new Microsoft.AspNetCore.Http.Endpoint(requestDelegate: null, metadata, "test"));
        if (routeSiteId is { } siteId)
        {
            http.Request.RouteValues[SiteContext.RouteValue] = siteId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var context = new Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext([requirement], principal, http);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
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



    private sealed record Row(long Id, long SiteId);
}
