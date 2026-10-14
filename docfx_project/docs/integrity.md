# Integrity signatures

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
  **This holds only when the key ring is out of that person's reach.** With the default database ring
  (E8.6, no `Wms:DataProtection:KeyRingPath`), the ring's key material sits in `wms.data_protection_key`
  unprotected, so anyone who can read the database can recover the integrity key and sign a changed row
  that then verifies; the guarantee shrinks to accidental and limited-privilege changes, and the API logs a
  Warning at every start saying so. For the full guarantee keep the ring in a directory only the
  application account can read (`Wms:DataProtection:KeyRingPath`); protecting the ring with DPAPI or a
  certificate is #1078.
- A migration script or a bulk fix that changes security rows by accident: the verification job reports
  the rows at Error, with table and id, on its next run.

**Not guarded against**

- An attacker who can run code inside the application or read its Data Protection ring: they hold the key
  and can sign anything. The ring's protection (file permissions, or the database ring reachable only by
  the application account) is the boundary.
- Deleting rows. A missing assignment or role only removes access; the audit trail keeps the history.
  Deleting an assignment or a role directly in the database is **not a supported way to revoke access**:
  nothing is left to verify, so it cannot be detected, and a session already open keeps the grant it was
  issued with until it ends. Revoke through the console or the API, which ends the user's sessions at once
  (E10.5).
- Deleting the key row (`wms.integrity_key`) and restarting: the next start creates a new key and signs
  every row as it stands (the upgrade path below). Treat a "key created" log entry on a database that is
  not new as an incident; keep database-level auditing and backups for that case.
- Fields outside the signed set (display names, descriptions, timestamps, `updated_by`): changing them
  alters nothing a decision depends on.

## What is signed

| Table | Signed fields |
|---|---|
| `core.user` | normalised user name, password hash, must-change-password, disabled, local administrator, sessions-valid-after, failed sign-in count, locked-until |
| `core.role` | normalised name, built-in key, the sorted permission names of its `core.role_permission` rows |
| `core.user_role` | user id, role id, site id, expires-at |

Every signature also covers the row's server-assigned id, so a row's content and signature cannot be
moved to another id: swapping two users' rows would otherwise put a known password on the id that holds
the grants. A new row is inserted unsigned and signed by a second save as soon as the insert has given it
an id; if the process stops between the two, the new row fails verification and has to be recreated. The
signature column is `signature` (base64, 44 characters), never audited.

## The key

One row of `wms.integrity_key`: 32 random bytes, created on the first start that finds none, stored under
its own Data Protection purpose (`Wolfgang.Wms.Integrity.v1`) in the `enc:v1:` form. Every instance that
shares the ring reads the same key; an instance on a different ring cannot verify anything and refuses
every sign-in. Back the row up with the database and the ring together ([Configuration](configuration.md)).

## Verification

- On sign-in: the user row (before anything is read from it); a failure answers
  `403 auth.integrity_failure` and is logged at Warning with the user name.
- On every request: the session check verifies the user row before it reads `is_disabled` and
  sessions-valid-after, so a row changed outside the application ends that user's sessions (`401`) at once.
  Reading an account (`FindAsync`) and changing a password verify it too; a failed row is never returned
  or re-signed.
- When grants are computed (sign-in, password change): every assignment and its role; a failure drops
  that assignment silently for the user and logs an Error with the table and id.
- On a schedule: the worker's verification job walks every signed row every
  `auth.integrity.verify_interval` (a setting; 1 minute by default, 10 seconds to 7 days; the tables are
  small) and logs a Warning summary with the failed and checked counts, Information when clean. A session
  carries the grants it was issued with, so for every user holding a failed assignment or a failed role the
  job ends their open sessions (sessions-valid-after), logged at Warning; they sign in again with what still
  verifies. A tampered grant therefore lasts at most one interval in an open session. The last result is kept for the
  health endpoint (E12.1).

Every failure is logged at Error as `Integrity failure: <table> row <id> does not match its signature`.
Alert on that message.

## Repair

A row that fails verification stays failed until someone deliberately rewrites it: change the account or
assignment through the console or the API (which re-signs on save), or delete and recreate it. A write the
application makes on its own never touches a failed row, so it cannot re-sign the change by accident: ending a
user's sessions after a role change, the verification job ending sessions, and the start-up grant of the
Administrator role to local administrators (a failed user gets no grant) all verify the row first. A built-in role
that fails verification is never re-seeded at start-up (its key may have been moved onto another role):
it grants nothing until the row is restored from a backup. There is no
"re-sign everything" command by design; if a whole database must be re-signed (a restored backup from a
different ring), delete the key row and restart once, and record why.

## Upgrade

The first start after E10.4 finds no key: it creates one and signs every existing row, logging a Warning
with the count. The key and the signatures commit in one transaction that locks `wms.integrity_key`, so a
start that stops partway leaves no key and the next start does the whole backfill again, and an instance
starting at the same moment waits for it and then uses the same key. From then on an unsigned row is a
failure.
