type: feature

Schema conventions applied to the EF model on both providers: module schemas, snake_case names, server-assigned `long` `id` keys, `<table>_id` foreign keys with explicit indexes and no cascades, `decimal(9,3)` quantities, UTC `DateTimeOffset` timestamps (`datetime2(3)` / `timestamptz(3)`); owned types included; a model test rejects any violation (including key/constraint/index names and client-generated ids), and a Docker test reads both engines' catalogs back.
