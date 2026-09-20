# ADR 0005: One session cookie on the shared key ring; providers behind one interface

**Status:** Accepted (E9) · **Date:** 2026-09-20

## Context

The console (Blazor Server), the handheld app and integrations all reach the one API (E82.1). Console users
sign in locally at first (E9) and through OIDC and other providers later (E11); pickers use badge/PIN and
integrations API keys (E10.5), which are separate. Several API instances must serve one session (E12.6), the
break-glass administrator must keep working when SSO is broken (E9.3), and switching providers must be a
configuration task (E11.0).

## Decision

- **The API issues the console session** as an encrypted cookie (`wms.session`, HttpOnly, SameSite=Lax,
  sliding, absolute lifetime from `auth.session.lifetime`) protected by the shared Data Protection ring
  (E8.6), so any instance reads any session and the console, served from the same origin behind the reverse
  proxy, carries it on every API call. The API never redirects: an unauthenticated call is `401
  auth.not_signed_in`, a forbidden one `403 auth.forbidden`.
- **Local accounts are one provider among several.** `ILocalAccounts` (E9) validates credentials and issues
  the same claims every provider will (`SessionClaims`); E11.0's provider interface adds challenge/callback
  providers that end in the same cookie. The claims carry the user id, names and the must-change flag; roles
  and site scopes (E10) are claims too, so authorization never re-reads the user row per request.
- **Passwords** are hashed with ASP.NET Core Identity's `PasswordHasher` (PBKDF2-HMAC-SHA512, iterated,
  upgradable in place), never audited, never logged. Lockout after `auth.local.lockout_threshold` failures
  for `auth.local.lockout_duration`; every attempt is logged at Warning; the sign-in endpoint is rate-limited
  per address.
- **Bootstrap** creates the administrator once with a documented default password and a must-change flag
  enforced by middleware, so an installer has no per-install secret to communicate and no way to skip the
  change.
- **Not chosen:** ASP.NET Core Identity's full user manager and schema (its tables, roles and claims model do
  not fit the site-scoped assignments of E10.3 or the audit and signature rules of E6.4/E10.4); JWTs for the
  console (a cookie the browser handles beats a token the page must store); separate sessions per host.

## Consequences

- The console needs no auth code of its own beyond a login page and forwarding the cookie; the API is the
  one place identity is decided.
- Every instance must share the key ring (already required by E8.6); a rotated ring signs everyone out.
- Per-user "sessions valid after" (E10.5) is checked on the cookie's principal against the user row through a
  cached lookup, not on every request from the database.
