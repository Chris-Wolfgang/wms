# Troubleshooting

Every error the API answers with carries a stable `code` and a `type` link into this page (E82.3). Find your
code below: what it means, and what to do. The full list with statuses and messages is the
[error codes reference](reference/error-codes.md); `ReferenceDocsTests` fails the build when a code has no
entry here.

## Sign-in and sessions (`auth.*`)

<a id="auth-invalid-credentials"></a>
### `auth.invalid_credentials`
The user name or password is wrong. The message is the same for an unknown user and a wrong password, so
names cannot be probed. Check the spelling and the keyboard layout; an administrator can reset the password.

<a id="auth-locked-out"></a>
### `auth.locked_out`
Too many failed sign-ins locked the account until the time in the message (`auth.lockout_minutes`). Wait
for the lock to expire, or ask an administrator to reset the password, which clears the lock.

<a id="auth-disabled"></a>
### `auth.disabled`
The account is disabled. Only an administrator can enable it again.

<a id="auth-integrity-failure"></a>
### `auth.integrity_failure`
The account row does not match its integrity signature: it was changed outside the application or the
integrity key changed. Sign-in is refused as a precaution. An administrator repairs it as described in the
integrity guide (re-sign after verifying the row, or restore from backup).

<a id="auth-not-signed-in"></a>
### `auth.not_signed_in`
The request carried no valid session. Sign in; a session also ends when it expires (`auth.session.lifetime`)
or when an administrator revokes it.

<a id="auth-forbidden"></a>
### `auth.forbidden`
The signed-in user lacks the permission the endpoint checks, everywhere or at the request's site. A role
that carries the permission (see the [permissions reference](reference/permissions.md)) must be assigned.

<a id="auth-password-change-required"></a>
### `auth.password_change_required`
The account still has its bootstrap or reset password. Change it through the console (or
`POST /auth/local/password`); nothing else is allowed until then.

<a id="auth-password-rejected"></a>
### `auth.password_rejected`
A password change was refused: the current password is wrong or the new one fails the policy (length,
not the user name, not the current password). The message says which.

<a id="auth-provider-not-enabled"></a>
### `auth.provider_not_enabled`
The named identity provider is not in `auth.providers.enabled` or is not registered on this host. Enable it
in the settings, or use a provider the sign-in page offers.

<a id="auth-provider-failed"></a>
### `auth.provider_failed`
The identity provider refused or failed the sign-in; the message names the reason without any secret. Check
the provider's configuration (`auth.oidc.*`), its reachability from the API host, and the provider's own logs.

<a id="auth-mapping-rejected"></a>
### `auth.mapping_rejected`
A group-to-role mapping was refused: the group is blank or the same mapping already exists.

<a id="auth-mapping-not-found"></a>
### `auth.mapping_not_found`
No group mapping has that id; it was removed, or the id is from another environment.

<a id="auth-unavailable"></a>
### `auth.unavailable`
Sign-in is unavailable until a database is configured. Complete the bootstrap (`Wms:Database:*`) and restart.

## Roles and assignments (`auth.roles.*`)

<a id="auth-role-not-found"></a>
### `auth.role_not_found`
No role has that id.

<a id="auth-role-name-taken"></a>
### `auth.role_name_taken`
A role with that name exists; names are unique. Pick another name or edit the existing role.

<a id="auth-built-in-role-read-only"></a>
### `auth.built_in_role_read_only`
Built-in roles cannot be edited or deleted. Copy the role (`POST /auth/roles/{id}/copy`) and edit the copy.

<a id="auth-unknown-permission"></a>
### `auth.unknown_permission`
The role names a permission that is not in the catalog. Only the permissions in the
[permissions reference](reference/permissions.md) can be granted.

<a id="auth-invalid-role"></a>
### `auth.invalid_role`
The role draft is incomplete: the message names the missing or invalid field (name, description).

<a id="auth-assignment-not-found"></a>
### `auth.assignment_not_found`
No assignment has that id; it was already removed.

<a id="auth-user-not-found"></a>
### `auth.user_not_found`
No user has that id.

## Concurrency (`concurrency.*`)

<a id="concurrency-precondition-failed"></a>
### `concurrency.precondition_failed`
The `If-Match` tag sent is not the record's current version: someone else changed it since it was read.
Reload the record, reapply the change and send the new ETag.

<a id="concurrency-precondition-required"></a>
### `concurrency.precondition_required`
An update or delete arrived without `If-Match`. Read the record first and send its ETag, so a stale copy
can never overwrite a newer one.

## Devices (`device.*`)

<a id="device-version-missing"></a>
### `device.version_missing`
The request carried no `X-Wms-Device-Version` header. The handheld app sends it on every call; a tool
calling device endpoints must send its version too.

<a id="device-version-invalid"></a>
### `device.version_invalid`
The header value is not a version (`major.minor.patch`).

<a id="device-version-too-old"></a>
### `device.version_too_old`
The app is older than the site's minimum (`device.min_app_version`). Update the app; nothing else works
until then.

## Licensing (`license.*`)

<a id="license-key-rejected"></a>
### `license.key_rejected`
The pasted key did not verify or is not a key of this release's schema; the message says why (not a signed
document, signature does not verify, unknown tier, newer schema, missing field). Paste the document exactly
as issued; a key for a newer schema needs an upgrade first. See the [licensing guide](licensing.md).

<a id="license-key-not-found"></a>
### `license.key_not_found`
No installed key has that id. The compiled-in free key cannot be removed.

<a id="license-limit-reached"></a>
### `license.limit_reached`
A creation is blocked by a license limit: beyond the allowance, after the grace period, or while coverage
has lapsed. The message names the limit and the tier. Install a larger key or an add-on, or remove
something that counts; nothing already created stops working.

<a id="license-feature-not-licensed"></a>
### `license.feature_not_licensed`
The feature is not in the installed tier. The [feature comparison](licensing/feature-comparison.md) shows
which tier includes it.

## Logging (`logging.*`)

<a id="logging-elevation-rejected"></a>
### `logging.elevation_rejected`
A timed level elevation was refused: the level must lower the threshold (Trace, Debug or Information) and
the duration must be between 1 minute and `logging.elevation_max_minutes`.

## Settings (`settings.*`)

<a id="settings-unknown-key"></a>
### `settings.unknown_key`
No module declares a setting of that name. The [settings reference](reference/settings.md) lists every one;
names are case-sensitive.

<a id="settings-scope-not-allowed"></a>
### `settings.scope_not_allowed`
The setting cannot be configured at that scope (its declaration lists the allowed scopes). Configure it at
an allowed scope; descendants inherit.

<a id="settings-unknown-scope"></a>
### `settings.unknown_scope`
The scope type in the URL is not one of `organization`, `site`, `zone`, `sku`.

<a id="settings-store-unavailable"></a>
### `settings.store_unavailable`
A write arrived before a database was configured. Settings are read-only defaults until the bootstrap
completes.

<a id="settings-mode-not-allowed"></a>
### `settings.mode_not_allowed`
The cascade mode requested is not allowed for that setting at that scope (for example delegating below the
lowest allowed scope).

<a id="settings-decided-elsewhere"></a>
### `settings.decided_elsewhere`
An ancestor delegated the decision past this scope, so the value is decided lower down (the message says
where and by whom). Configure it there, or change the ancestor's cascade mode back to `value`.

<a id="settings-invalid-value"></a>
### `settings.invalid_value`
The value does not parse as the setting's kind or the validator rejected it; the message says what is
expected.
