# Database conventions

What a database administrator finds in the WMS schema. The rules are the same on SQL Server and PostgreSQL,
and the product checks them every time it is built, so a module cannot ship a table that breaks them.

## Schemas

Every table lives in a schema named after its module (`picking`, `layout`, `settings`, …). Nothing is
created in `dbo` (SQL Server) or `public` (PostgreSQL).

## Names

Tables, columns, keys, foreign keys and indexes are lower-case snake_case on both engines, so hand-written
SQL needs no quoting.

| Object | Name | Example |
|--------|------|---------|
| table | singular noun | `container`, `zone_group` |
| primary key column | `id` | `container.id` |
| foreign key column | `<referenced table>_id` | `container.zone_group_id` |
| primary key | `pk_<table>` | `pk_container` |
| alternate key | `ak_<table>_<columns>` | `ak_sku_code` |
| foreign key | `fk_<table>_<columns>` | `fk_container_zone_group_id` |
| index | `ix_<table>_<columns>`; unique `ux_…` | `ix_container_zone_group_id`, `ux_container_barcode` |

Every identifier is at most 63 characters. That is PostgreSQL's limit, and it is enforced on SQL Server too, so a
name is never truncated on one engine and kept on the other; a conventional name that would run past it is
given a shorter explicit name by the module that owns the table.

Detail rows that belong to one parent (for example container lines) get their own table keyed by
`(<parent table>_id, id)`. A small value stored with its parent (an address, say) is a set of prefixed
columns on the parent's table (`ship_to_street`).

## Referential integrity

Every relationship is a real foreign key, including across schemas, and **no foreign key cascades**:
deleting a referenced location, SKU or zone fails in the database. Detail rows are removed by the
application together with their parent; their constraint is still `NO ACTION` in the database.

## Keys and types

- Primary keys are `bigint` identity columns assigned by the database. There are no GUID columns.
- Timestamps are UTC with millisecond precision: `datetime2(3)` on SQL Server, `timestamp(3) with time zone`
  on PostgreSQL. They are for display and reporting; the product never uses them to detect changes.
- Quantities are `decimal(9,3)` (`numeric(9,3)` on PostgreSQL), up to 999,999.999.
- Natural keys (ERP release id, barcodes) have unique indexes, and every foreign key column is indexed.
