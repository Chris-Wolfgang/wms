type: feature

Schema conventions applied to the EF model on both providers: module schemas, snake_case names, server-assigned `long` `id` keys, `<table>_id` foreign keys with explicit indexes and no cascades, `decimal(9,3)` quantities, UTC `DateTimeOffset` timestamps (`datetime2(3)` / `timestamptz(3)`); a model test rejects any violation.
