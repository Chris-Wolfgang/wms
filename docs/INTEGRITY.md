# Integrity signatures (E10.4)

The security tables are signed. A row of `core.user`, `core.role` (with its `core.role_permission` rows)
or `core.user_role` carries an HMAC-SHA256 over its security-relevant fields, computed by the application
on every save and checked before the row is honoured. A row edited through the database, without the key,
fails the check and is treated as absent: the user cannot sign in, the assignment grants nothing, the role
grants nothing.

## Threat model

**Guarded against**

- A database administrator, a leaked connection string or a compromised backup used to grant permissions,
  enable a disabled account, clear a password-change gate, extend an assignment or add a permission to a
  role, without going through the application (which audits, authorises and logs every such change).
- A migration script or a bulk fix that changes security rows by accident: the verification job reports
  the rows at Error, with table and id, on its next run.

**Not guarded against**

- An attacker who can run code inside the application or read its Data Protection ring: they hold the key
  and can sign anything. The ring's protection (file permissions, or the database ring reachable only by
  the application account) is the boundary.
- Deleting rows. A missing assignment or role only removes access; the audit trail keeps the history.
- Deleting the key row (`wms.integrity_key`) and restarting: the next start creates a new key and signs
  every row as it stands (the upgrade path below). Treat a "key created" log entry on a database that is
  not new as an incident; keep database-level auditing and backups for that case.
- Fields outside the signed set (display names, descriptions, timestamps, `updated_by`): changing them
  alters nothing a decision depends on.

## What is signed

| Table | Signed fields |
|---|---|
| `core.user` | normalised user name, password hash, must-change-password, disabled, local administrator, sessions-valid-after |
| `core.role` | normalised name, built-in key, the sorted permission names of its `core.role_permission` rows |
| `core.user_role` | user id, role id, site id, expires-at |

The server-assigned id is not signed (it does not exist when the row is signed). Copying one row's
signed content over another gains nothing: user and role names are unique, and an assignment names its
user. The signature column is `signature` (base64, 44 characters), never audited.

## The key

One row of `wms.integrity_key`: 32 random bytes, created on the first start that finds none, stored under
its own Data Protection purpose (`Wolfgang.Wms.Integrity.v1`) in the `enc:v1:` form. Every instance that
shares the ring reads the same key; an instance on a different ring cannot verify anything and refuses
every sign-in. Back the row up with the database and the ring together (docs/CONFIGURATION.md).

## Verification

- On sign-in: the user row; a failure answers `403 auth.integrity_failure` and is logged at Warning with
  the user name.
- When grants are computed (sign-in, password change): every assignment and its role; a failure drops
  that assignment silently for the user and logs an Error with the table and id.
- On a schedule: the worker's verification job walks every signed row every
  `auth.integrity.verify_interval` (a setting; 1 hour by default, 1 minute to 7 days) and logs a Warning
  summary with the failed and checked counts, Information when clean. The last result is kept for the
  health endpoint (E12.1).

Every failure is logged at Error as `Integrity failure: <table> row <id> does not match its signature`.
Alert on that message.

## Repair

A row that fails verification stays failed until the application rewrites it: change the account or
assignment through the console or the API (which re-signs on save), or delete and recreate it. There is no
"re-sign everything" command by design; if a whole database must be re-signed (a restored backup from a
different ring), delete the key row and restart once, and record why.

## Upgrade

The first start after E10.4 finds no key: it creates one and signs every existing row, logging a Warning
with the count. From then on an unsigned row is a failure.
