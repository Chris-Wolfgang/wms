type: feature

Domain takes a dependency on Wolfgang.TryPattern for `Result<T>` and gains a purity guard: allow-listed assembly references, no async members, and an IL scan rejecting I/O, network, data and process calls.
