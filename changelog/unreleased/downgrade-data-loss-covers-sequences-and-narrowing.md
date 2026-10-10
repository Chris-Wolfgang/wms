type: fix

`wms-migrate` asks for `--confirm-data-loss` on a downgrade that drops a sequence (the row-version sequence is every client's sync watermark), updates rows, or narrows a column; before, only dropped tables, columns, schemas, deleted rows and raw SQL were counted, so `--to Initial` dropped `wms.row_version_seq` unconfirmed.
