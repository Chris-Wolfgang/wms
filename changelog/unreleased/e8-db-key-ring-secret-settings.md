type: feature

Secrets at rest: the Data Protection key ring is stored in the database (`wms.data_protection_key`) by default so every instance shares one ring without a shared volume (a directory ring remains the option for single-node installs and encrypted connection strings); secret-kind settings are stored encrypted (`enc:v1:`) and decrypted only for the typed read.
