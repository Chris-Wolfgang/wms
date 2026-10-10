type: fix

`wms-migrate --status` exits by what it found (`0` up to date, `5` behind, `4` unreachable, newer or inconsistent) so a script can gate on it, reports `Pending: unknown` instead of every shipped migration when the database cannot be queried, and Ctrl+C (`130`), an unwritable `--output` (`2`) and a failure before any migration ran (`1`) end with a documented exit code and one line instead of a stack trace.
