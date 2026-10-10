type: fix

A module's explicit `ToTable(name, schema)` name is honoured (snake_cased) for every entity; `ModelConventions.Apply` no longer replaces a non-owned table's explicit name with the CLR type name while keeping the schema from the same call.
