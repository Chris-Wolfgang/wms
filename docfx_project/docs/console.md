# The console

The console (`Wolfgang.Wms.Web`) is the browser front end for office and floor staff. It is one site that
hosts five workspaces, each for a different kind of work.

## Workspaces

| Workspace | Address | For | Tier |
|-----------|---------|-----|------|
| Configure | `/configure` | Setting up sites, zones, users and settings | Free |
| Supervise | `/supervise` | Watching and steering live picking | Free |
| Resolve | `/resolve` | Resolving totes and exceptions at the resolution lane | Free |
| Report | `/report` | Rates, totes on the line, completion times and device metrics | Free |
| Insights | `/insights` | Findings and recommendations for improving the floor | Paid |

## Entering a workspace

Two things must both be true before a user can enter a workspace:

- **The license includes it.** The free tier has Configure, Supervise, Resolve and Report. Insights needs a
  paid license. Without it, the workspace shows "… is not licensed" and names the missing license feature.
- **The user has permission.** Each workspace has its own enter permission (`workspace.<name>.enter`).
  Without it, the workspace shows "… is not available to you" and names the permission the user's roles lack.

The navigation bar lists only the workspaces the user can enter. When the user opens the console at `/`, a
user who can enter exactly one workspace goes straight to it. Everyone else picks from a list.

## Tethered scanners

A keyboard-wedge scanner (one that types the barcode and then presses Enter) works on every workspace screen.
The scan field takes the input. If focus ends up nowhere, for example after a click on an empty part of the
page, it returns to the scan field, so the next scan is not lost; typing in another field on the screen is not
interrupted. If the screen you are on does not use scans, the console says so ("Scan … is
not used on this screen.") instead of silently dropping the scan.

## When a screen fails

A fault in one screen does not take the console down. The workspace shows a message in place of that screen
with a **Retry** button that renders the screen again; the workspace header, the scanner and the other
workspaces keep working. A scan that the screen could not handle is reported next to the scan field ("Scan …
could not be handled by this screen.") and the next scan clears the message.

## Language

The console's text comes from resource files, and the language follows the user's language choice, then the
browser's language. English is the only language in this version.
