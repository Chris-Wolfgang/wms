type: fix

A connection string the provider cannot parse fails configuration validation naming the setting (instead of a stack trace from the first database context), the API's schema startup check is registered whatever the provider and fails startup when the provider was changed after registration, and the schema endpoint's source logs why the migrations history could not be read.
