type: feature

Database provider selection at install time: `Wms:Database:Provider` (`SqlServer` or `PostgreSql`), connection string and `TrustServerCertificate` from configuration or environment; an unknown provider fails startup with a clear message; the schema endpoint reads the migrations history once a provider is configured.
