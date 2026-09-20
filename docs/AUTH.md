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

## What comes next

- E9.3: local sign-in disabled once SSO is verified and re-enabled for a timed window from the host only.
- A periodic job that audits expired assignments.
- E11: OIDC and other providers behind one interface, chosen in the console.
