type: feature

Add `wms-migrate`: apply pending migrations (exit code names a failing migration), `--to` a migration in either direction with `--confirm-data-loss` for destructive downgrades, `--script [--from] [--to]` for idempotent provider-specific SQL without a connection, and `--status`; the API refuses to start on a schema that is behind, ahead or unreachable unless `Wms:Database:AutoMigrate` is on; the migrations history table moves to schema `wms`.
