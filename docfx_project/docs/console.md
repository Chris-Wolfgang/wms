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
The scan field takes the input. If the screen you are on does not use scans, the console says so ("Scan … is
not used on this screen.") instead of silently dropping the scan.

## Language

The console's text comes from resource files, and the language follows the user's language choice, then the
browser's language. English is the only language in this version.
