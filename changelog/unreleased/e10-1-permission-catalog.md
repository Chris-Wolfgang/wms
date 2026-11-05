type: feature

Permission catalog: every module declares its permissions, every endpoint declares the permission it requires (or that it is anonymous), the catalog is served at `GET /auth/permissions`, sessions carry grants per organisation or site, and the local administrator holds every permission.
