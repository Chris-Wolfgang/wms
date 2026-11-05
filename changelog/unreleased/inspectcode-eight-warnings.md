type: internal

The eight InspectCode warnings open on `main` are gone: two redundant casts and two always-true nullable checks in the migration narrowing code, two closures that captured a disposable past its disposal (the migrate tool's Ctrl+C handler, a cache test), and two sample-model key accessors that only EF Core touches.
