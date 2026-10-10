type: feature

Master data import contract: one file per entity type (zones, locations) as a JSON array of the API's own objects plus `action`, loaded by `POST /sites/{siteId}/imports/{entity}` as an idempotent upsert by natural key under `all_or_nothing`, `accept_valid_rows` or `validate_only`, answered with one result shape (counts plus a line per row, also as CSV), with the field definitions generated into the docs from the row types (E16.6).
