# Signing in and identity providers

The console can offer more than one way to sign in. Which ones it offers, and in what order, is a setting
you change at runtime; no restart is needed.

## Choosing the sign-in methods

- **`auth.providers.enabled`** (a setting, organization level): the providers the login page offers, comma
  separated, in the order shown. The default is `local` (user name and password). Add `oidc` once single
  sign-on is configured. Every server picks up a change within about 5 seconds. A name that is not an
  installed provider is logged and skipped.
- Changing a provider's own settings (for example rotating its client secret) takes effect without a
  restart as well.

## If a provider change locks everyone out

Set `Wms:Auth:ForceLocal` to `true` in `appsettings.json` and restart. The login page then offers only
`local`, whatever `auth.providers.enabled` says, so an administrator can sign in and repair the provider
settings. Remove the override afterwards: it is a recovery switch, not a way to choose providers.

## Endpoints

| Endpoint | Who | What it does |
|---|---|---|
| `GET /api/v0/auth/providers` | anyone | The enabled providers for the login page, with the URL that starts each redirect sign-in. |
| `GET /api/v0/auth/{provider}/challenge?returnUrl=` | anyone | Starts a redirect provider's sign-in. Only a path on this site is honoured as `returnUrl`; anything else returns to `/`. Answers `404 auth.provider_not_enabled` for a provider that is not enabled. |
| `POST /api/v0/auth/providers/{name}/check` | `auth.providers.manage` | Tests the provider's connection (for single sign-on, its discovery document). |
