type: feature

Setting registry: `SettingKey<T>` now carries kind, allowed scopes, validator, restart and device-resync flags and a codec (invariant-culture stored text for bool, integers, numbers, enums, strings, durations, timestamps, secrets, JSON); the host builds a `SettingRegistry` from every module's declared settings, refuses duplicates and unknown keys, and serves `GET /settings/registry`.
