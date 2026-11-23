# Architecture Decision Records

This folder records the design decisions behind this repository that the code cannot explain on its own. See
[ADR 0000](0000-record-architecture-decisions.md) for the practice, and copy [`TEMPLATE.md`](TEMPLATE.md) to add a
record.

A record is not rewritten once **Accepted**. A reversed decision gets a new record that supersedes the old one, and
the old one stays with its status updated. Numbers never change and are never reused; a retired number keeps its row
below.

| # | Title | Status |
|---|-------|--------|
| [0000](0000-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0001](0001-modular-monolith.md) | Modular monolith with vertical slices over a thin clean core | Accepted |
| [0002](0002-data-conventions.md) | Data conventions — single tenant, unit of work, repositories per aggregate, site scoping | Accepted |
| [0003](0003-read-models-and-caching.md) | Read models, per-instance caching and HTTP validation by row version | Accepted |
| [0004](0004-api-client-generator.md) | API client generated with Kiota from the committed OpenAPI document | Accepted |
| [0005](0005-identity-topology.md) | One session cookie on the shared key ring; providers behind one interface | Accepted |

## Status legend

- **Accepted**: in force.
- **Superseded by NNNN**: replaced by a later record; kept for history.
- **Deprecated**: no longer applies, and nothing replaced it.
- **Retired**: the number was used and the record removed; the number is not reused.
