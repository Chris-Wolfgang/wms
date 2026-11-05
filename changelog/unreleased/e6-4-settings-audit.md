type: feature

Audit trail: `WmsDbContext` is an AuditTrail `AuditingDbContext`, so every save of an audited entity (settings first) writes `core.audit_header` / `core.audit_detail` rows in the same transaction, attributed to the host's application name and the signed-in user; transient failures are retried on both providers.
