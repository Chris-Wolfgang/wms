# Database conventions (E3)

What a DBA sees in the schema, on either engine, and how it is enforced. `ModelConventions`
(`Wolfgang.Wms.Infrastructure.Database.Conventions`) applies the naming and typing rules to every entity when the
model is built; `ModelConventions.Verify` lists everything a module's configuration can still break, and the
model test (`ModelConventionsTests`) asserts the product model has no violations for both providers.
`SchemaCatalogTests` (Docker) creates a convention-built model on SQL Server and PostgreSQL and reads the
catalogs (`INFORMATION_SCHEMA`, `sys.indexes`, `pg_indexes`) back: schemas, table/column/constraint/index
names, identity columns, delete rules and column types are what the DBA actually gets.

The DBA-facing page is `docfx_project/docs/database-conventions.md` (published as "Database conventions");
keep the two in step.

## Schemas (E3.1)

Every table lives in a module schema (`picking`, `layout`, `settings`, …) named in the module's entity
configuration (`ToTable("Container", "picking")`). Nothing lands in `dbo` or `public`; the verifier rejects
an entity without a schema or in a default schema.

## Names (E3.2)

Tables, columns, keys, foreign keys and indexes are snake_case on both engines (`SnakeCase.Of`), so
hand-written SQL needs no quoting tricks:

| Object | Name | Example |
|--------|------|---------|
| table | singular CLR name | `container`, `zone_group` |
| primary key column | `id` | `container.id` |
| attribute column | unprefixed in its own table | `container.type` |
| foreign key column | `<principal table>_id` | `container.zone_group_id` |
| primary key | `pk_<table>` | `pk_container` |
| alternate key | `ak_<table>_<columns>` | `ak_sku_code` |
| foreign key | `fk_<table>_<columns>` | `fk_container_zone_group_id` |
| index | `ix_<table>_<columns>`, unique `ux_…` | `ix_container_zone_group_id`, `ux_container_barcode` |

The verifier checks key, foreign-key constraint and index names as the database sees them: the right prefix
and snake_case.

Owned types follow the same rules. An owned collection (or an owned value given its own table) gets a
snake_case table (`container_line`) keyed by `(<owner table>_id, id)`; an owned value sharing its owner's table
leaves that table and its `id` alone and keeps EF's navigation prefix on its columns (`ship_to_street`).

## Referential integrity (E3.3)

Every relationship is a real foreign key, across schemas, with `Restrict` delete behaviour: deleting a
referenced location, SKU or zone fails in the database, not just in the application. No cascades. The one
exception in EF terms is ownership: an owned row is part of its owner, so the ownership foreign key is
`ClientCascade` (EF deletes the owned rows with the owner; the database constraint is still `NO ACTION`). A
`Restrict` ownership would make every owner delete fail in the change tracker. The verifier checks this for every ownership,
including an owned value that shares its owner's table and so has no database constraint.

## Keys, types and indexes (E3.4)

- Primary keys are server-assigned `long` identity columns; clients never generate ids (the verifier rejects a
  key with a client-side value generator, `HasValueGenerator` / `HasValueGeneratorFactory`, and an id with a
  column default or computed value, `HasDefaultValue(Sql)` / `HasComputedColumnSql`; an owned collection's own
  `id` must be generated on add too); no GUID columns anywhere.
- Timestamps are `DateTimeOffset` in UTC, stored as `datetime2(3)` on SQL Server (through
  `UtcDateTimeOffsetConverter`) and `timestamptz(3)` on PostgreSQL. They are for display and reporting only:
  change detection uses `row_version` (E5), journal ordering uses `device_seq`, and time-ordered pagination
  pairs the timestamp with `id` as the tiebreaker (E82.3). A timestamp is never a sync watermark.
- Quantities are `decimal(9,3)` (5 bytes on SQL Server, max 999,999.999); integer-only behaviour comes from
  `qty_precision`, not the column type.
- Client-originated records carry `(device_id, device_seq)` (events) or `(device_id, run_seq)` (deposits) as
  unique idempotency keys (E5, E40).
- Natural keys (`erp_release_id`, `barcode`, tote barcode) are unique indexes; every foreign key column is
  indexed explicitly on both providers (EF creates them; the verifier rejects a missing one).

## Adding an entity

1. Class in the module with `long Id`, `DateTimeOffset` timestamps, `decimal` quantities, `<Principal>Id`
   foreign keys.
2. `IEntityTypeConfiguration` with `ToTable("Name", "<module>")`, unique indexes for natural keys.
3. `scripts/Check-Migrations.ps1 -Add <Name>`; review both providers' migrations; cite the query each new
   secondary index serves in a comment (E3.5).
4. The model test fails the build if any convention is broken.
