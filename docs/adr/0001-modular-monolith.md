# ADR 0001: Modular monolith with vertical slices over a thin clean core

**Status:** Accepted (E1.10) · **Date:** 2026-09-19

## Context

The product is one installation per customer (E1.11) that starts with picking and will add replenishment and
inventory later. Customers run only what they license (E79) and want to add or omit modules cleanly. A thin
handheld client shares the picking rules with the server (E1.4). Operational simplicity matters more than
independent deployability: one binary, one database, one installer.

## Decision

1. **One deployable, several modules.** `Wolfgang.Wms.Api` (and `Worker` for the same code out of process) hosts
   modules such as `Picking`, later `Replenishment` and `Inventory`. Module boundaries are enforced by
   architecture tests: modules interact only through public contracts, never through each other's internals or
   tables, and **a module references only `Core` and `Domain`, never another module**.
2. **Vertical slices inside a module.** One feature folder per capability holding the endpoint, handler,
   validator and models for that capability. No horizontal layers inside a module.
3. **Thin clean-architecture core.** `Wolfgang.Wms.Domain` is dependency-free and shared with the device (E1.4
   purity guard). `Wolfgang.Wms.Core` holds host-side plumbing every module uses: the module descriptor and
   collection, and later jobs (`Core.Jobs`: `IJob`, scheduler, leader lock, run history) and Core-owned jobs
   (outbox sender, retention, backup, integrity check). `Infrastructure` holds EF Core and providers.
4. **Event sourcing only where it pays.** The deposit journal is event-sourced; everything else is plain
   state with row versions (E5).
5. **Explicit registration, no assembly scanning.** A module exposes `AddPickingModule()`-style extension
   methods that build a `ModuleDescriptor` and add it to the host's `ModuleCollection`
   (`services.AddWmsModule(descriptor)`); `app.MapWmsModules()` applies every descriptor in registration
   order. The descriptor lists the module's contributions as typed values: endpoints today; jobs, settings,
   permissions, issue types, navigation, EF configurations, resources and error codes as their key types land
   (E1.13, E6, E10, E12). Nothing is discovered by reflection, which also keeps the host AOT-friendly (E1.9).
6. **Jobs are hosted services owned by the module.** `Core.Jobs` provides `IJob`, the scheduler, the leader
   lock, run history (`core.job_run`), "run now", enable/disable. Module jobs live in the module
   (`Wolfgang.Wms.Picking.Jobs`) and register via the descriptor. `Wolfgang.Wms.Worker` is a host only, so
   jobs can also run inside the API process on a one-box install. A job that fails N times or misses twice
   its interval raises an issue. No cron, no queue: tables are the queue.

## Consequences

- Adding a module means one project, one `Add…Module()` method, and entries in the descriptor; the host
  changes by one line. Removing it is the reverse.
- The architecture tests (`ModuleDependencyTests`, `DomainPurityTests`, `ConversionMethodPlacementTests`)
  are the enforcement; a violation fails the build, not a review.
- Splitting a module into its own process later is possible because modules already talk only through
  contracts, but it is not a goal.
- The Blazor console (`Web`) talks to the API over HTTP (E82.4); it is not a module host.
