type: fix

`wms-migrate --to <name>` matches a migration name in any case, and a migrations history with a gap (a pending migration older than the last applied one) is refused by the tool (exit 4) and the API's startup check with one message naming the migrations and the repair, instead of "Nothing to do." on one side and "schema is behind" on the other.
