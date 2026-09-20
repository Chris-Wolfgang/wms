type: feature

Roles and assignments: the five built-in roles (Administrator, Supervisor, Resolver, Support, Viewer) are seeded from the permission catalog on every start and are read-only but copyable; custom roles are built from catalog permissions; a role is assigned everywhere or at one site, optionally until a date, and sign-in turns active assignments into grants (`GET/POST/PUT/DELETE /auth/roles`, `/auth/users/{id}/roles`, `/auth/assignments/{id}`).
