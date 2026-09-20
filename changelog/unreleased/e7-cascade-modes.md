type: feature

Settings cascade: a scope can delegate a setting downward (`per_site`, `per_zone`, `per_sku`) instead of holding a value, scopes an ancestor delegated past are refused (409), and a newly created scope can be populated with every inherited value so no record is unresolved.
