type: feature

API conventions: problem-details errors built from typed error codes (`ApiProblems`), Brotli/gzip response compression with request decompression and a per-endpoint opt-out, `Idempotency-Key` rules and store contract, and keyset pagination types (`PageRequest`, `Page<T>`, `Cursor`) with per-endpoint sortable fields in either direction (`sort`, `PageSorting`); a cursor is bound to the sort it was issued under.
