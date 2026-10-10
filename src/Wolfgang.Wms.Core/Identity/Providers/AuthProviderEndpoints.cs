// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolfgang.Wms.Core.Api;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Core.Settings;
using External = Wolfgang.Wms.Core.Identity.External;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Identity.Providers;

/// <summary>
/// The provider endpoints of the <c>auth</c> module (E11.0): the enabled list for the login page, the
/// challenge that starts a redirect provider's sign-in, and the health check for administrators.
/// </summary>
public static class AuthProviderEndpoints
{
    /// <summary>Route of the enabled-provider list.</summary>
    public const string ProvidersRoute = "/auth/providers";

    /// <summary>Route of one provider's health check.</summary>
    public const string CheckRoute = "/auth/providers/{name}/check";

    /// <summary>Route that starts a challenge provider's sign-in.</summary>
    public const string ChallengeRoute = "/auth/{provider}/challenge";

    /// <summary>Route of one provider's group-to-role mappings (E11.2).</summary>
    public const string GroupsRoute = "/auth/providers/{name}/groups";

    /// <summary>Route of one group-to-role mapping (E11.2).</summary>
    public const string GroupRoute = "/auth/providers/groups/{mappingId}";



    /// <summary>
    /// Configure identity providers: run health checks (the settings themselves need <c>settings.write</c>).
    /// </summary>
    public static readonly Permission Manage = new("auth.providers.manage", "Configure identity providers and test their connections");



    /// <summary>
    /// Maps the endpoints.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(ProvidersRoute, (AuthProviderState state) => TypedResults.Ok(state.Enabled))
            .AllowAnonymous()   // the login page asks before anyone is signed in
            .WithName("GetAuthProviders")
            .WithSummary("The enabled identity providers in login-page order (E11.0).");
        app.MapGet(ChallengeRoute, Challenge)
            .AllowAnonymous()   // the way in for a redirect provider
            .WithName("ChallengeAuthProvider")
            .WithSummary("Sends the browser to a challenge provider; 404 when the provider is not enabled.");
        app.MapPost(CheckRoute, CheckAsync)
            .RequirePermission(Manage)
            .WithName("CheckAuthProvider")
            .WithSummary("Runs a provider's health check (discovery for OIDC); 404 for an unknown provider.")
            .Produces<AuthProviderHealth>(StatusCodes.Status200OK);
        app.MapGet(GroupsRoute, ListGroupsAsync)
            .RequirePermission(Manage)
            .WithName("GetGroupRoleMappings")
            .WithSummary("A provider's directory groups mapped to roles (E11.2); users in no mapped group hold no role.")
            .Produces<IReadOnlyList<External.GroupRoleMappingInfo>>(StatusCodes.Status200OK);
        app.MapPost(GroupsRoute, AddGroupAsync)
            .RequirePermission(Manage)
            .WithName("AddGroupRoleMapping")
            .WithSummary("Maps a directory group to a role, everywhere or at one site; 409 when it exists.")
            .Produces<External.GroupRoleMappingInfo>(StatusCodes.Status201Created);
        app.MapDelete(GroupRoute, RemoveGroupAsync)
            .RequirePermission(Manage)
            .WithName("RemoveGroupRoleMapping")
            .WithSummary("Removes a group-to-role mapping; members lose the role at their next sign-in.")
            .Produces(StatusCodes.Status204NoContent);
    }



    private static async Task<IResult> ListGroupsAsync(string name, AuthProviderCatalog catalog, External.IGroupRoleMappings mappings, CancellationToken cancellationToken)
    {
        if (catalog.Find(name) is null)
        {
            return ApiProblems.Problem(AuthErrorCodes.ProviderNotEnabled, detail: null, name);
        }

        return TypedResults.Ok(await mappings.ListAsync(name, cancellationToken).ConfigureAwait(false));
    }



    private static async Task<IResult> AddGroupAsync(HttpContext http, string name, External.GroupRoleMappingDraft body, AuthProviderCatalog catalog, External.IGroupRoleMappings mappings, CancellationToken cancellationToken)
    {
        if (catalog.Find(name) is null)
        {
            return ApiProblems.Problem(AuthErrorCodes.ProviderNotEnabled, detail: null, name);
        }

        if (body is null)
        {
            throw new AuthException(AuthErrorCodes.MappingRejected, "A body with group and roleId is required.");
        }

        var mapping = await mappings.AddAsync(name, body, http.User.Identity?.Name ?? SettingsModule.AnonymousUser, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created(string.Empty, mapping);
    }



    private static async Task<IResult> RemoveGroupAsync(HttpContext http, long mappingId, External.IGroupRoleMappings mappings, CancellationToken cancellationToken)
    {
        await mappings.RemoveAsync(mappingId, http.User.Identity?.Name ?? SettingsModule.AnonymousUser, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }



    private static IResult Challenge(string provider, string? returnUrl, AuthProviderState state)
    {
        if (state.FindEnabledChallenge(provider) is null)
        {
            return ApiProblems.Problem(AuthErrorCodes.ProviderNotEnabled, detail: null, provider);
        }

        var redirect = returnUrl is not null && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal) ? returnUrl : "/";   // local URLs only
        return TypedResults.Challenge(new AuthenticationProperties { RedirectUri = redirect }, [provider]);
    }



    private static async Task<IResult> CheckAsync(string name, AuthProviderCatalog catalog, IServiceProvider services, CancellationToken cancellationToken)
    {
        var provider = catalog.Find(name);
        if (provider is null)
        {
            return ApiProblems.Problem(AuthErrorCodes.ProviderNotEnabled, detail: null, name);
        }

        return TypedResults.Ok(await provider.CheckAsync(services, cancellationToken).ConfigureAwait(false));
    }
}
