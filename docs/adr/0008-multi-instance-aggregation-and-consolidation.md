# ADR 0008: Multiple Free instances, an aggregation root, and site consolidation

**Status:** Accepted in outline (design discussion 2026-09-22); data-flow details go with the epic · **Date:** 2026-09-22 · **Builds on:** ADR 0002 (site scoping), ADR 0007 (Free floor)

## Context

Free is 1 site and 5 active devices with extra devices purchasable (E79.4). A customer with several
distribution centres can therefore run **one Free instance per DC**, buying devices where needed, and never
pay for a tier. What they do not get is anything cross-site: the Supervise and Report workspaces over all
DCs, shared master data, one place to configure. When they want that, there must be a path that does not
start with "re-enter everything". Site scoping (`site_id` on every site-scoped row, ADR 0002) is what makes
a site a movable unit; this ADR fixes the two moves.

## Decisions

### 1. Free instances are first-class, not a workaround

An organisation running N Free instances is a supported deployment. Each instance has its own installation
id, its own users, its own single site, and its own keys (Free base plus any purchased device add-ons).
Nothing in the product assumes it is the only instance, and nothing nags about it.

### 2. Path A: an aggregation root above the child instances

A **root instance** is a new, paid (Pro or Enterprise: cross-site monitoring and tooling is the paid benefit)
installation that **registers child instances** and mirrors each as one site.

- **Pairing** child to root uses the device-pairing mechanism (QR from the root: URL, one-time token,
  certificate fingerprint); the child stores the root as its "reports to" and pushes.
- **Data flows child → root only**, as change deltas over the row-version watermark (`?since=`, E5.3) and
  manifests (E5.4) — the same machinery devices use to sync. The root holds a read-only mirror per child
  site; the child stays the system of record and keeps working if the root is down.
- **Root workspaces** are Supervise and Report across all children, plus master-data views. No control
  actions flow root → child in this version (no picking, no settings push); that is a later decision with
  its own ADR because it changes who owns the data.
- **Identity** is per instance: root users are root users. A supervisor with rights on two children is two
  accounts until the identity epics give a cross-instance answer (OIDC makes this one identity provider
  anyway).
- **Licensing**: the root is a paid base key bound to the root's installation id; children keep Free plus
  their add-ons. Whether the root counts devices (it has no pickers of its own) is a commercial decision
  for Chris; the default is that the root counts none.

### 3. Path B: consolidation into one multi-site instance

The customer sets up a **new paid instance**, pulls each child's site into it, and deprecates the children.

- **Site export** produces a versioned package: every site-scoped row of that site plus the global rows it
  references (SKUs, barcodes, users, roles, settings overrides), with the source installation id and a
  manifest. **Site import** creates the site in the target and applies **merge rules** for global tables:
  SKUs and barcodes match by code (a conflict where the same code describes different products is reported,
  never guessed), users by email, roles by name, a child's site-level setting overrides become the site
  scope in the target. The import is a dry run first, with a report, then a commit.
- **Deprecation** of a child: the child goes read-only with a banner naming the target, its devices are
  re-paired to the target by QR, and it keeps its data for the customer's retention period. Nothing is
  deleted by the product.
- **Licensing**: the target needs a paid multi-site key against its own installation id; purchased device
  add-ons on the children are reissued against the target by sales (a supersede, E79.11), not recomputed by
  the product.
- Every imported site records its **origin** (source installation id, export time, package id) for audit
  and to make a second import of the same package a no-op.

### 4. Shared prerequisites

Both paths need: instance-to-instance pairing (the device-pairing transport reused), the change-delta and
manifest endpoints over every synced table (E5), a site export/import package format, a "site origin"
record, and a licensing operation to move add-ons. Path A additionally needs the read-only mirror and root
workspaces; Path B additionally needs the merge rules and the deprecation state.

## Consequences

- `site_id` stays the boundary for everything operational and export/import is built on it; a table that
  is neither clearly global nor site-scoped is a design error this ADR makes visible.
- Global tables (SKUs, users, settings) get a merge story; the schema needs stable natural keys (SKU code,
  barcode, email, role name) for it, which E3 already asks for.
- The root mirror is a consumer of the same delta/manifest API as devices, so that API must be complete
  over every table a workspace reads, not only the device tables.
- A customer can start Free per DC, grow into Path A for visibility, and later choose Path B; neither move
  loses data, and neither is required.
