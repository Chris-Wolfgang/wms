# ADR 0007: Evaluation and trial model — Free floor, trial keys, local trial first, database-per-visitor sandbox

**Status:** Accepted (design discussion 2026-09-22) · **Date:** 2026-09-22 · **Amends:** ADR 0002 (hosted model)

## Context

Prospects need to find out whether the product fits them before buying, on their own hardware where
possible. The device app runs on any Android device (a customer's scanners, or a phone or tablet), so an
evaluation is also a hardware check. Licensing (E79, ADR-level decisions in #137–#147) already gives every
installation a compiled-in Free tier, signed offline keys with an explicit tier table, soft limits with
allowance and grace, and freeze-on-lapse for paid coverage. This ADR adds what an evaluation needs on top of
that and fixes the demo topology, without weakening the single-tenant rule of ADR 0002.

## Decisions

### 1. Free is the trial of Free; a trial key exists only to try Pro or Enterprise

Free (1 site, 5 devices, unlimited users, locations and SKUs, no expiry) is compiled in and needs nothing
from the vendor. A prospect who wants Free installs and uses it; there is no separate demo of Free. A
**trial key** is a base key for the `pro` or `enterprise` tier with `trial: true`, a short coverage period
(30 to 60 days, sales chooses), an explicit `devices` limit (default 10, above the 5 included at every tier
so several phones and scanners can be tested at once), no organization or installation binding, and
otherwise unlimited (sites, users, locations, SKUs). Renewal is a new trial key with a later coverage end;
the newest base key wins, as E79.11 already rules. A trial key applied over a running Free install keeps
every row: it is an upgrade in place, and buying is the same operation with a paid key.

Free requires **no key** (decided 2026-09-22): a compiled-in tier cannot lapse or be revoked and needs
nothing from the vendor, which answers the "what if the vendor disappears" question completely and keeps the
install path at "install, run". The lead is captured instead by an optional **"Register this product"**
button on the console (name, company, email, sent with the installation id to the vendor endpoint; never
required, never nagged beyond one dismissible banner; EV.11), by the trial request, and by the update check
(decision 6).

### 2. Trial expiry reverts to Free and may park; paid lapse freezes (unchanged)

E79.5 keeps its rule for **paid** keys: nothing is removed on lapse, upgrades outside coverage are refused.
A **trial** is the one exception to "nothing already created ever stops working": when a trial key's
coverage ends the installation reverts to the Free tier, Pro/Enterprise features switch off, and anything
above a Free limit is **parked, never deleted**. Parking is chosen by the customer, not by the system:

- Sites: the console asks which site stays active (Free allows 1). Parked sites are visible, readable and
  exportable; no new releases, no picking; a device assigned to a parked site sees "site parked" instead of
  tasks. Locations, SKUs and history inside a parked site are untouched.
- Devices: the active-device count (E79.4) above 5 blocks new enrolment through the normal soft-limit path;
  enrolled devices keep working. No device is parked.
- Until the customer chooses, everything stays active behind a persistent banner; there is no midnight
  lockout. Applying a key that raises the limit un-parks immediately.

Warnings at 14, 7 and 1 days before the end read "Pro features end on …", not "your license expires".
The same parking step is the one mechanism for any future limit a revert can cross (users, integrations).

### 3. Paid keys bind to an installation id; trial and Free do not

On first run the installation generates a random **installation id**, stored in the database (so it
survives backup, restore, migration and hardware replacement; no hardware fingerprinting) and shown on the
console's License page with a copy button. Orders quote it; a paid key carries `installation_id` in its
signed payload and is accepted only where the id matches. A key without the field (trial, Free add-ons)
is accepted anywhere. Nothing the customer can do from the console changes the id; only a fresh database
does. This is a fence against accidental reuse, not fraud, and is not made stronger than that.

### 4. Local trial is the primary evaluation path

The product is sold as one installation per customer, so the honest demo is running it on the prospect's
own box with their scanners on their network. What that requires, all of it product work:

- **Trivial install**: one Windows installer or one compose file; a first-run wizard (admin, site, time
  zone, optional key, "load the demo warehouse").
- **Demo warehouse seed pack**: SKUs (a few dozen), locations, open releases; the simulator generates
  picker activity so the console is alive within a minute. "Reset to seed" exists only while the seed is
  present and no paid key is installed; it never changes the installation id.
- **Go live**: a one-time wizard, offered only while demo data is present, that removes demo operational
  data (SKUs, locations, releases, history, devices) and keeps settings, users, sites, keys and the
  installation id. It is never available on a warehouse with real history.
- **Device pairing by QR** from the console: server URL, one-time enrolment token, and the server
  certificate's fingerprint. Android refuses plain HTTP and a LAN install has no public certificate, so the
  app pins the fingerprint from the QR (single-use enrolment is a Free feature; the QR is its transport).
- **Barcode input** behind one seam with three backends: hardware scanner (keyboard wedge or vendor intent
  such as DataWedge), device camera, manual entry. Most local-trial visitors bring a phone.
- **Hardware check** screen on the device: detect a wedge scanner, fire a test scan, report model, Android
  version, screen, battery, network. Also a support tool.
- **One APK** for evaluation and production; demo behaviour comes from the server (seed, key), never from
  a build flavour. Distribution: Play listing plus the APK attached to the GitHub release. The app checks
  the server's minimum version (E82.7) and says "update the app" plainly.

### 5. The cloud sandbox is one process, one database per visitor — never a `tenant_id`

For prospects who want to look before involving IT, a hosted sandbox provisions **one database per
visitor** on a shared machine served by **one process**. The schema is exactly the product schema: no
`tenant_id`, site scoping unchanged, users, devices and the license inside the visitor's database. The
host resolves the database before anything else runs, from the subdomain (`v7k3.demo.<host>`) or the
pairing code a device was given. What this needs in the product, and only this:

- A **database resolver seam** in Infrastructure: host name or pairing token → connection string, read by
  the `DbContext` factory per request. Single-tenant installs resolve to one fixed connection; the product
  path does not change.
- **Per-database instances** of every per-instance cache (`VersionedCache<T>`, ADR 0003); a process-wide
  cache is a bug in a multi-database host.
- **Worker jobs iterate databases** (lease expiry, outbox, rollups): same code, outer loop.
- **Provisioning is a script**, not a feature: create database, migrate, seed the demo warehouse, issue an
  Enterprise trial key with a short clock, return the subdomain and pairing QR; expiry drops the database.
  A cost cap and abuse controls (rate limits, one sandbox per requester) live in the provisioning layer.
  Release-time migrations run across every live visitor database.

A single shared demo installation used by many strangers at once is ruled out: it breaks the single-tenant
rule, every visitor sees every other visitor's data, and it is the least representative of what they buy.

### 6. Stale-install detection is telemetry, not licensing

The update check the console already needs ("an update is available") sends the installation id and the
product version, nothing else, to a public endpoint. That is the active-install signal; a Free install that
never checks in for a year is gone. The check is visible and switchable in settings ("Check for updates",
on by default, payload documented). Blocked outbound traffic simply means the install is not counted.

## Consequences

- The tier table gains no new tier: a trial is a `pro` or `enterprise` base key with `trial: true` and
  explicit limits. The key schema gains `trial` and `installation_id`; both optional, so existing keys and
  the compiled-in Free base are unaffected.
- E79.5 splits into two behaviours (paid lapse freezes; trial end reverts and parks), and the parking step
  is new product surface on the console and in the device task list.
- The database resolver seam, per-database caches and per-database worker loops are small, but they must
  exist before the first cache or job is written process-wide; DomainPurity-style architecture tests guard
  that no cache is registered as a process singleton keyed on nothing.
- ADR 0002's hosted model changes from "one database and one Kubernetes namespace per customer" to "one
  database per customer; a shared process is allowed". That is also the architecture if hosting is ever
  sold.
- The evaluation stories (seed pack, simulator start, reset, go-live, QR pairing with pinning, barcode seam,
  hardware check, resolver seam, provisioning) are tracked in the "Evaluation" epic; the licensing changes
  (trial keys, installation id, trial expiry, issuing tool) in E79.
