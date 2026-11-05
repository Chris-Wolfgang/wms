# Signing in and identity providers

A company signs in one way: an install uses one identity provider, chosen when it is installed. Two
providers cannot run at once. Under single sign-on, the local administrator remains as the way back in if the
provider fails.

## Choosing the identity provider

- **`auth.provider`** (a setting, organization level): the install's one provider. The default is `local`
  (user name and password). Set it to `oidc` once single sign-on is configured. A name that is not an
  installed provider is logged and no provider is offered.
- Changing to another provider later is a reconfiguration of the system: configure the new provider, set up
  its users and groups, and plan a window for the change. Do not rely on switching during the working day
  without an outage; restart the application as part of the change if needed.
- Changing a provider's own settings (for example rotating its client secret) takes effect without a restart.

## If a provider change locks everyone out

Set `Wms:Auth:ForceLocal` to `true` in `appsettings.json` and restart. The login page then offers only
`local`, whatever `auth.provider` says, so an administrator can sign in and repair the provider
settings. Remove the override afterwards: it is a recovery switch, not a way to choose providers.

## Endpoints

| Endpoint | Who | What it does |
|---|---|---|
| `GET /api/v0/auth/providers` | anyone | The active provider for the login page, as a list of at most one, with the URL that starts a redirect sign-in. |
| `GET /api/v0/auth/{provider}/challenge?returnUrl=` | anyone | Starts a redirect provider's sign-in. Only a path on this site is honoured as `returnUrl`; anything else returns to `/`. Answers `404 auth.provider_not_enabled` for a provider that is not enabled. |
| `POST /api/v0/auth/providers/{name}/check` | `auth.providers.manage` | Tests the provider's connection (for single sign-on, its discovery document). |
