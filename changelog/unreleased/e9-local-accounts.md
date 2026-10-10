type: feature

Local accounts: `core.user` with PBKDF2 password hashes, the bootstrap administrator created once on first run with a documented default password that must be changed before anything else is reachable, lockout after the configured number of failures, and `POST /auth/local/login`, `POST /auth/logout`, `GET /auth/me`, `POST /auth/local/password` with a session cookie protected by the shared key ring.
