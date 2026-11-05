type: fix

`VersionedCache.Invalidate()` is honoured even when a read is already probing or loading: that read no longer republishes the pre-commit copy over the invalidation, so a writer's change is visible on the next read instead of after the next poll interval.
