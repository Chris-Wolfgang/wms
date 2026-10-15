type: fix

The `auth.integrity.verify_interval` setting's description now says the worker reports failures; sessions are not revoked by it, because their grants are recomputed on every request.
