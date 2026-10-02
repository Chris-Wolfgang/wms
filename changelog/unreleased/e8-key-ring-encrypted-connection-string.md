type: feature

Secrets: the Data Protection key ring lives at `Wms:DataProtection:KeyRingPath` (created on first run for the running user only); the connection string may be stored encrypted as `enc:v1:...` (`wms-migrate --protect` produces it) and a missing or wrong key ring fails startup with a clear message; `ISecretProtector` is the one interface every secret goes through, Data Protection by default; environment variables override both values.
