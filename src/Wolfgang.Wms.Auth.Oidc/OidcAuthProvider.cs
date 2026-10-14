// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Wolfgang.Wms.Core.Identity.Providers;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Auth.Oidc;

/// <summary>
/// The <c>oidc</c> provider (E11.1): the ASP.NET OpenID Connect handler configured from settings. The
/// settings are read into a <see cref="OidcSnapshot"/> whenever they change (or on enablement), the cached
/// handler options are dropped, and the next request builds new ones; nothing needs a restart. The health
/// check reads the discovery document.
/// </summary>
public sealed partial class OidcAuthProvider : IChallengeAuthProvider
{
    /// <summary>
    /// The named HTTP client the discovery check uses.
    /// </summary>
    public const string DiscoveryClient = "wms-oidc-discovery";



    private readonly ILogger<OidcAuthProvider> _logger;
    private OidcSnapshot _snapshot = OidcSnapshot.Default;



    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
    public OidcAuthProvider(ILogger<OidcAuthProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }



    /// <inheritdoc/>
    public string Name => OidcSettings.ProviderName;



    /// <inheritdoc/>
    public string DisplayName => Snapshot.DisplayName;



    /// <inheritdoc/>
    public AuthProviderKind Kind => AuthProviderKind.Challenge;



    /// <inheritdoc/>
    public IReadOnlyList<SettingKey> Settings => OidcSettings.All;



    /// <inheritdoc/>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type HandlerType => typeof(OpenIdConnectHandler);



    /// <summary>
    /// The settings as last read.
    /// </summary>
    public OidcSnapshot Snapshot => Volatile.Read(ref _snapshot);



    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="principal"/> is null.</exception>
    public ExternalIdentity Identify(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var snapshot = Snapshot;
        var subject = principal.FindFirst("sub")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var userName = principal.FindFirst(snapshot.NameClaim)?.Value ?? principal.FindFirst("email")?.Value ?? subject;
        var displayName = principal.FindFirst(snapshot.DisplayNameClaim)?.Value ?? userName;
        var groups = principal.FindAll(snapshot.GroupClaim).Select(c => c.Value).Where(v => v.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        return new ExternalIdentity(subject, userName, displayName, groups);
    }



    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public async Task ApplyAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var snapshot = await OidcSnapshot.ReadAsync(services.GetRequiredService<ISettings>(), cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _snapshot, snapshot);
        services.GetRequiredService<IOptionsMonitorCache<OpenIdConnectOptions>>().TryRemove(Name);   // the next request builds options from the new snapshot
        LogApplied(_logger, snapshot.Authority.Length > 0 ? snapshot.Authority : "(none)", snapshot.IsConfigured);
    }



    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public async Task<AuthProviderHealth> CheckAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var snapshot = await OidcSnapshot.ReadAsync(services.GetRequiredService<ISettings>(), cancellationToken).ConfigureAwait(false);
        if (!snapshot.IsConfigured)
        {
            return new AuthProviderHealth(Healthy: false, Detail: "Set auth.oidc.authority and auth.oidc.client_id first.");
        }

        try
        {
            using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(DiscoveryClient);
            var retriever = new HttpDocumentRetriever(client) { RequireHttps = snapshot.RequireHttps };
            var configuration = await OpenIdConnectConfigurationRetriever.GetAsync(snapshot.Authority + "/.well-known/openid-configuration", retriever, cancellationToken).ConfigureAwait(false);
            return new AuthProviderHealth(Healthy: true, Detail: $"Discovery read from {snapshot.Authority}: issuer {configuration.Issuer}, {configuration.SigningKeys.Count} signing keys.");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or ArgumentException or TaskCanceledException)
        {
            return new AuthProviderHealth(Healthy: false, Detail: "Discovery failed: " + exception.Message);
        }
    }



    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC provider settings applied: authority {Authority}, configured {Configured}.")]
    private static partial void LogApplied(ILogger logger, string authority, bool configured);
}
