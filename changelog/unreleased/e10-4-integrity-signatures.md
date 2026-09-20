type: feature

Integrity signatures: users, roles (with their permissions) and role assignments carry an HMAC over their security-relevant fields, computed on every save with a per-installation key stored protected in `wms.integrity_key`; a row changed outside the application is not honoured (`403 auth.integrity_failure` on sign-in, no grants from the assignment or role) and is logged at Error; the worker re-verifies every signed row every `auth.integrity.verify_interval`; the first start after the upgrade creates the key and signs the existing rows (docs/INTEGRITY.md).
