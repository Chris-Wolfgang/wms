# Keycloak as the console's identity provider (E11.3)

How to point a Wolfgang.Wms install at Keycloak. The recipe is the realm `KeycloakSignInTests` imports on
every pull request, so it is verified against a real Keycloak (26.x) on every change.

## In Keycloak

1. **Realm.** Any realm; `wms` below.
2. **Client.** `Clients → Create client`: type *OpenID Connect*, client ID `wms-console`. On *Capability config*
   turn **Client authentication** on (a confidential client with a secret) and leave **Standard flow** on;
   **Direct access grants** off. Under *Advanced → Proof Key for Code Exchange Code Challenge Method* choose
   `S256` (the console always sends PKCE). *Valid redirect URIs*: `https://<console host>/auth/oidc/callback`;
   *Web origins*: the console's origin. Copy the secret from the *Credentials* tab.
3. **Group claim.** On the client, *Client scopes → wms-console-dedicated → Add mapper → By configuration →
   Group Membership*: name `groups`, token claim name `groups`, **Full group path off**, add to ID token,
   access token and userinfo. Without this mapper the console sees no groups and nobody gets a role.
4. **Groups and users.** Create the groups you will map (`wms-supervisors`, …) and put users in them. Users
   need a username (`preferred_username`, the sign-in name in the console) and a first/last name (`name`, the
   display name).

The export of a realm built this way is in `tests/Wolfgang.Wms.IntegrationTests/Database/KeycloakSignInTests.cs`
(`RealmExport`); `kc.sh start-dev --import-realm` with that file in `/opt/keycloak/data/import/` reproduces it.

## In the console (Configure workspace, or the settings API)

| Setting | Value |
|---|---|
| `auth.oidc.authority` | `https://<keycloak host>/realms/wms` (discovery is read from `<authority>/.well-known/openid-configuration`) |
| `auth.oidc.client_id` | `wms-console` |
| `auth.oidc.client_secret` | the client's secret (stored encrypted) |
| `auth.oidc.scopes` | `openid profile email` (the default) |
| `auth.oidc.group_claim` | `groups` (the mapper's claim name) |
| `auth.oidc.name_claim` | `preferred_username` (the default) |
| `auth.oidc.display_name_claim` | `name` (the default) |
| `auth.oidc.require_https` | `true` outside a lab |
| `auth.providers.enabled` | `oidc,local` (or `oidc` alone once the break-glass gate is understood, E9.3) |

Then map groups to roles: `POST /auth/providers/oidc/groups` with `{ "group": "wms-supervisors", "roleId": … }`
(or the Configure workspace). *Test connection* (`POST /auth/providers/oidc/check`) must report the issuer.

## Checks when it does not work

- The console redirects to Keycloak but Keycloak says *Invalid parameter: redirect_uri*: the callback URL in
  the client's valid redirect URIs does not match `https://<console host>/auth/oidc/callback` exactly.
- Sign-in succeeds but the user has no permissions: the `groups` mapper is missing or has *Full group path*
  on (the claim then reads `/wms-supervisors`, which no mapping names), or no mapping exists for the group.
- `502 auth.provider_failed` naming the issuer: the authority does not match the `iss` Keycloak puts in the
  token (scheme, host, port and realm must be the same as Keycloak's frontend URL).
- The provider check fails with a certificate error: the API host does not trust Keycloak's certificate;
  `auth.oidc.require_https=false` is for labs only.
