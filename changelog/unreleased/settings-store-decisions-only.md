type: feature

Settings store only decisions (ADR 0009): `core.setting` holds a row where an administrator set a value or a cascade mode, and the value at any other scope is resolved on read from the nearest decision above it or the default. A change at a site writes one row, not a copy per zone and SKU; creating a site, zone or SKU writes no setting rows; a reset removes the decision. The `effective_value` column and `ISettings.PopulateAsync` are gone.
