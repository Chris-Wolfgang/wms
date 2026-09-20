# Authentication

How users sign in (E9; providers in E11; roles and permissions in E10; ADR 0005).

## Local accounts (E9.2)

A local account is a row of `core.user` with a password hash (ASP.NET Core Identity's PBKDF2 hasher; the
hash is never audited or logged). Sign-in is case-insensitive on the user name. After
`auth.local.lockout_threshold` consecutive failures (default 5) the account is locked for
`auth.local.lockout_duration` (default 15 minutes); a success resets the count. Every attempt is logged at
Warning with its outcome, and the sign-in endpoint allows 20 attempts per address per minute before `429`.

| Endpoint | Meaning |
|----------|---------|
| `POST /auth/local/login` `{ "userName", "password" }` | `200` and the session cookie; `401 auth.invalid_credentials` (unknown name and wrong password alike), `423 auth.locked_out` (with the time), `403 auth.disabled`, `503 auth.unavailable` before a database exists. |
| `GET /auth/me` | `{ userId, userName, displayName, mustChangePassword, isLocalAdmin }`; `401 auth.not_signed_in` otherwise. |
| `POST /auth/local/password` `{ "currentPassword", "newPassword" }` | `200` and a refreshed cookie; `400 auth.password_rejected` with the reason (wrong current password, shorter than 12 characters, the documented default). |
| `POST /auth/logout` | Ends the session. |

The session is a cookie (`wms.session`, HttpOnly, SameSite=Lax) protected by the shared Data Protection key
ring, so every API instance reads it; it slides on activity and expires after `auth.session.lifetime`
(default 8 hours, at most 24). The API never redirects to a login page: the console shows one and calls
these endpoints.

## Bootstrap administrator (E9.1)

The first start after the schema is installed creates the administrator named by
`Wms:Bootstrap:AdminUserName` (default `admin`) with the documented default password:

```
ChangeMe-2026!
```

It exists only to be replaced. Until the administrator changes it, every request except the password change,
sign-out and who-am-I answers `403 auth.password_change_required`; the default is refused as a new password.
Once any local administrator exists, the bootstrap values are ignored on later starts. The local administrator
can be disabled but never deleted (E9.3 keeps it as the break-glass account).

## Permissions (E10.1)

Every action has a named permission (`settings.write`, `workspace.configure.enter`), declared by the module
that owns it and listed at `GET /auth/permissions` (`name`, `description`, `module`). Every endpoint declares
the permission it requires with `RequirePermission(...)`, or says `AllowAnonymous()` with a comment naming
why (the product name, the schema probe, sign-in and sign-out, device sync until device tokens arrive); an
architecture test fails the build for an endpoint that says neither.

A session carries its grants as `wms:permission` claims: `name@organization` holds everywhere,
`name@site:<id>` at one site only, `*` stands for every permission. A request acts on the site named by its
`siteId` route value or the `X-Wms-Site` header; without one, only organisation grants satisfy it. Holding a
permission at site A never grants it at site B (E10.3). The local administrator holds `*@organization`;
other users' grants come from their roles (E10.2). A missing session answers `401 auth.not_signed_in`, a
missing permission `403 auth.forbidden`; `GET /auth/me` lists the session's grants.

## Roles and assignments (E10.2, E10.3)

A role is a named set of catalog permissions. Five built-in roles exist from the first start and follow the
catalog on every start: each permission names the built-in roles that hold it by default
(`Permission.DefaultRoles`), so a new module's permissions land in the right roles without a migration;
**Administrator** holds `*`. Built-in roles are read-only: copy one (`POST /auth/roles/{id}/copy`) to get an
editable role, then change its name, description and permissions. **Resolver** holds only the resolution
lane's permissions (as those modules declare them), never settings or master data; **Support** and
**Viewer** read.

| Endpoint | Meaning |
|----------|---------|
| `GET /auth/roles`, `GET /auth/roles/{id}` | Roles, built-in first (`builtIn` carries the key). `auth.roles.read`. |
| `POST /auth/roles` `{ name, description, permissions[] }` | A role from catalog permissions; `400 auth.unknown_permission`, `409 auth.role_name_taken`. `auth.roles.write`. |
| `PUT /auth/roles/{id}` (If-Match) | Replaces name, description and permissions; `409 auth.built_in_role_read_only`. |
| `POST /auth/roles/{id}/copy` `{ name }` | An editable copy; the administrator's copy lists every permission explicitly so it can be trimmed. |
| `DELETE /auth/roles/{id}` | Deletes a custom role and its assignments. |
| `GET /auth/users/{userId}/roles` | The user's assignments, active and expired. |
| `POST /auth/users/{userId}/roles` `{ roleId, siteId?, expiresAt? }` | Assigns the role everywhere (`siteId` null) or at one site, optionally until a date; the same role at the same scope is replaced. |
| `DELETE /auth/assignments/{id}` | Removes an assignment. |

Permissions are evaluated per site (E10.3): a user has an action at a site only if a role assigned to them
*for that site* (or everywhere) grants it; holding a permission at site A never grants it at site B. At
sign-in the active assignments become grants (`name@organization`, `name@site:3`); an expired assignment
grants nothing and is listed as expired. The local administrator additionally holds `*@organization` and is
assigned Administrator on every start. Sites are not entities yet, so a site id is a plain number until
they arrive.

## Sessions (E10.5)

A console session ends when any of these is reached, all read from settings so an administrator balances
security and convenience without a restart:

| Rule | Setting | Default |
|------|---------|---------|
| Absolute lifetime, from sign-in | `auth.session.lifetime` | 8 hours (5 minutes to 24 hours) |
| Idle timeout, sliding on any request (a dashboard's auto-refresh counts) | `auth.session.idle_timeout` | 30 minutes (1 minute to 24 hours) |

Every user carries a "sessions valid after" instant: a password change, disabling the account, or any role
change (assign, unassign, delete a role) sets it to now, so sessions and tokens issued before it are
refused on their next request (`401 auth.not_signed_in`) and the user signs in again with the new grants.
The check is one primary-key read per request. The password-change endpoint re-signs the caller in, so their
own session continues.

Picker tokens and integration API keys (bearer header, hashed at rest, two active keys during rotation)
arrive with the device and integration stories; provider sign-out (front-/back-channel) with E11.

## Hardening (E10.6)

- Sign-in is rate-limited per address and locks the account after repeated failures; every attempt, every
  password change and every role change is logged at Warning and audited through the store.
- `api.cors.allowed_origins` (a setting) lists the browser origins allowed to call the API; listed origins
  may send the session cookie and read `ETag`; nothing is allowed until an origin is listed; changes apply
  on the next request.
- `Wms:Hosting:BehindProxy` makes both hosts honour the reverse proxy's forwarded scheme and address
  (docs/CONFIGURATION.md), so cookies are secure and addresses real behind Caddy or IIS.
- The console sends on every response: a content-security policy tuned for Blazor Server (`default-src
  'self'`, inline styles only, the circuit's WebSocket, `frame-ancestors 'none'`), `X-Content-Type-Options:
  nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-Frame-Options: DENY` and a minimal
  `Permissions-Policy`; an integration test checks them.
- Pending: picker PIN and API-key rate limits (with those credentials), JIT provisioning (E11), and the
  compose smoke test of the proxy setup (E14).

## Integrity signatures (E10.4)

Users, roles and assignments are signed on save and verified before they are honoured; a row changed
through the database grants nothing and cannot sign in (`403 auth.integrity_failure`). The worker
re-verifies every signed row every `auth.integrity.verify_interval`. Threat model, signed fields, key
handling and repair: docs/INTEGRITY.md.

## Identity providers (E11.0)

Providers sit behind one interface (`IAuthProvider`; challenge providers also `IChallengeAuthProvider`):
a name, a display name, a kind (credentials posted to the API, or a challenge the browser is sent to), a
settings schema (the keys the console renders as the provider's configuration block, declared by the
`auth` module so they are registered, validated and audited like any setting) and a health check ("test
connection"). Implementations are registered in DI at startup, each provider kind in its own project
(`local` is built into Core; `oidc` arrives with E11.1); adding a kind is a release.

Which providers the console offers is the `auth.providers.enabled` setting (comma-separated, in
login-page order; default `local`). It is applied at runtime: a challenge provider's authentication scheme
is added to `IAuthenticationSchemeProvider` when the setting names it and removed when it is dropped, and a
provider whose settings changed is told to drop its cached handler options; every instance re-reads the
setting every 5 seconds. Names that are not registered are logged and skipped.

- `GET /auth/providers` (anonymous): the enabled providers for the login page, each with its kind and,
  for a challenge provider, the URL that starts its sign-in.
- `GET /auth/{provider}/challenge?returnUrl=` (anonymous): starts a challenge provider's sign-in; only
  local return URLs are honoured; `404 auth.provider_not_enabled` otherwise.
- `POST /auth/providers/{name}/check` (`auth.providers.manage`): the provider's health check.
- `Wms:Auth:ForceLocal=true` in `appsettings` is the emergency override: only `local` is offered whatever
  the setting says (a lock-out recovery after a broken provider change), never the normal selection.

Pickers (badge/PIN) and integrations (API keys) are separate from console providers and arrive with the
device and integration stories.

## OpenID Connect (E11.1)

The `oidc` provider (`Wolfgang.Wms.Auth.Oidc`, registered by `AddWmsOidcProvider()` in the API host) is
the ASP.NET Core OpenID Connect handler configured from settings, never from `appsettings`:

| Setting | Default | Meaning |
|---|---|---|
| `auth.oidc.display_name` | `Single sign-on` | The login-page label |
| `auth.oidc.authority` | empty | The issuer URL; discovery is read from `<authority>/.well-known/openid-configuration` |
| `auth.oidc.client_id` | empty | The client registered at the provider |
| `auth.oidc.client_secret` | empty | Secret kind: encrypted at rest, masked on screen; empty for a public client (PKCE only) |
| `auth.oidc.scopes` | `openid profile email` | Space-separated; `openid` is always sent |
| `auth.oidc.group_claim` | `groups` | The claim carrying the directory groups |
| `auth.oidc.name_claim` | `preferred_username` | The claim used as the sign-in name (falls back to `email`, then the subject) |
| `auth.oidc.display_name_claim` | `name` | The claim used as the display name |
| `auth.oidc.require_https` | `true` | Refuse discovery over plain HTTP; off only in a lab |

A change is picked up within 5 seconds: the provider re-reads the settings and drops its cached handler
options; nothing restarts. The flow is authorization code with PKCE; the callback is `/auth/oidc/callback`
(host-relative, answered by the handler); claims are kept as the provider sends them (`sub`, `groups`,
...) and the userinfo endpoint is read. "Test connection" (`POST /auth/providers/oidc/check`) reads the
discovery document and reports the issuer and the signing keys.

**Accounts.** A validated token becomes a console session through `IExternalAccounts`: one `core.user`
row per (provider, subject), created on first sign-in (sign-in name from the name claim; `@oidc` appended
when a local account already holds it), display name refreshed on every sign-in, no password. Disabled
accounts and rows that fail their integrity signature are refused. A failure at the provider (state
mismatch, token error, discovery down) answers `502 auth.provider_failed`.

**Group-to-role mapping (E11.2).** `core.group_role_mapping` maps a provider's group identifier (as the
provider sends it: an object id, a name, a DN) to a role, everywhere or at one site. On every provider
sign-in the account's assignments are replaced by exactly what its groups map to, so a user in no mapped
group holds no role and a mapping removed in the console takes effect at the next sign-in (other sessions
of that user are revoked when the set changes). Manual assignments to a provider account are overwritten
at sign-in: manage their access in the directory. API: `GET`/`POST /auth/providers/{name}/groups`,
`DELETE /auth/providers/groups/{id}` (`auth.providers.manage`); the console page rides E82's Configure
workspace.

**Verification (E11.3).** Every PR runs the full flow against `ghcr.io/navikt/mock-oauth2-server` in a
container (challenge, provider redirect, callback with the correlation and nonce cookies, back-channel
token exchange, mapping applied and removed, forged state → 502). Keycloak (realm import) and the Entra ID
scheduled job, with their setup guides, follow in E11.3–E11.5.

## What comes next

- E9.3: local sign-in disabled once SSO is verified and re-enabled for a timed window from the host only.
- A periodic job that audits expired assignments.
- E11.3–E11.6: Keycloak and Entra ID verification jobs, ADFS checklist, setup guides.
