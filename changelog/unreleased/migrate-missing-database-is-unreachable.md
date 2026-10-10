type: fix

`wms-migrate` reports a database that does not exist on the server as unreachable and refuses to apply (exit 4) instead of creating it: the DBA creates the database, the tool only fills it, so a mistyped name no longer ends in a stray database created and migrated under the DBA's login.
