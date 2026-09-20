// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.Auth.Oidc;

/// <summary>
/// The OIDC provider's settings (E11.1): authority, client, scopes and claim names, all changeable without
/// a restart; the client secret is a Secret-kind setting (encrypted at rest, masked on screen).
/// </summary>
public static class OidcSettings
{
    /// <summary>
    /// The provider name.
    /// </summary>
    public const string ProviderName = "oidc";



    /// <summary>What the login page shows for the provider.</summary>
    public static readonly SettingKey<string> DisplayName = new("auth.oidc.display_name", "Single sign-on", "The label of the OIDC sign-in option on the login page.")
    {
        Scopes = SettingScopes.Organization,
        Validator = v => v is { Length: > 0 and <= 64 } ? null : "must be 1 to 64 characters",
    };

    /// <summary>The issuer URL, where discovery lives.</summary>
    public static readonly SettingKey<string> Authority = new("auth.oidc.authority", string.Empty, "The OpenID Connect issuer (authority) URL; discovery is read from it.")
    {
        Scopes = SettingScopes.Organization,
        Validator = v => v.Length == 0 || (Uri.TryCreate(v, UriKind.Absolute, out var uri) && (string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) || string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase))) ? null : "must be an absolute http(s) URL",
    };

    /// <summary>The client (application) id registered at the provider.</summary>
    public static readonly SettingKey<string> ClientId = new("auth.oidc.client_id", string.Empty, "The client id registered at the provider.")
    {
        Scopes = SettingScopes.Organization,
        Validator = v => v.Length <= 256 ? null : "must be at most 256 characters",
    };

    /// <summary>The client secret.</summary>
    public static readonly SettingKey<SecretText> ClientSecret = new("auth.oidc.client_secret", new SecretText(string.Empty), "The client secret (stored encrypted); empty for a public client with PKCE only.")
    {
        Scopes = SettingScopes.Organization,
    };

    /// <summary>The scopes requested, space-separated.</summary>
    public static readonly SettingKey<string> Scopes = new("auth.oidc.scopes", "openid profile email", "The scopes requested, space-separated; openid is always included.")
    {
        Scopes = SettingScopes.Organization,
        Validator = v => v.Length <= 512 ? null : "must be at most 512 characters",
    };

    /// <summary>The claim carrying the person's group identifiers.</summary>
    public static readonly SettingKey<string> GroupClaim = new("auth.oidc.group_claim", "groups", "The claim type carrying the directory groups used for role mapping.")
    {
        Scopes = SettingScopes.Organization,
        Validator = ClaimName,
    };

    /// <summary>The claim used as the sign-in name.</summary>
    public static readonly SettingKey<string> NameClaim = new("auth.oidc.name_claim", "preferred_username", "The claim type used as the console sign-in name.")
    {
        Scopes = SettingScopes.Organization,
        Validator = ClaimName,
    };

    /// <summary>The claim used as the display name.</summary>
    public static readonly SettingKey<string> DisplayNameClaim = new("auth.oidc.display_name_claim", "name", "The claim type used as the display name.")
    {
        Scopes = SettingScopes.Organization,
        Validator = ClaimName,
    };

    /// <summary>Whether discovery must be served over HTTPS (off only for a lab).</summary>
    public static readonly SettingKey<bool> RequireHttps = new("auth.oidc.require_https", defaultValue: true, "Refuse a provider whose discovery document is not served over HTTPS; switch off only in a lab.")
    {
        Scopes = SettingScopes.Organization,
    };



    /// <summary>
    /// Every key, for the provider's settings schema and the module descriptor.
    /// </summary>
    public static IReadOnlyList<SettingKey> All { get; } = [DisplayName, Authority, ClientId, ClientSecret, Scopes, GroupClaim, NameClaim, DisplayNameClaim, RequireHttps];



    private static string? ClaimName(string value)
    {
        return value is { Length: > 0 and <= 128 } && !value.Any(char.IsWhiteSpace) ? null : "must be a claim type of 1 to 128 characters without spaces";
    }
}
