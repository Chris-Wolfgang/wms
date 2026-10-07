# wms-admin (E9.3)

The host-side tool that opens or closes the break-glass gate on local sign-in ([AUTH.md](AUTH.md)). It is its
own JIT executable (`src/Wolfgang.Wms.Admin`, assembly `wms-admin`) next to `wms-migrate`; there is no
`wms admin` subcommand on the NativeAOT CLI (a leading `admin` word is accepted and ignored).

## Command surface

| Command | Does |
|---------|------|
| `wms-admin unlock [--minutes N]` | opens local sign-in for `N` minutes (default 30, 1 to 43200; never open-ended, use a large value such as 1200 when a long window is needed); a new window replaces an open one |
| `wms-admin lock` | closes an open window now |
| `wms-admin status` | reports whether local sign-in is open and why, without changing anything |
| `--channel <name>` | the host's pipe name, overriding `Wms:Admin:ChannelName` (default `Wolfgang.Wms.Admin`) |
| `--key-ring <path>` | the host's key ring directory, overriding `Wms:DataProtection:KeyRingPath` |

Configuration is read from `appsettings.json` in the working directory and `Wms__*` environment variables;
flags win. Exit codes: `0` ok, `1` the host refused the command (reason printed), `2` usage or configuration
error, `3` the host did not answer on the channel.

## How it stays host-only

- The channel is a named pipe (a Unix domain socket off Windows) that only a process on the same machine can
  reach. On Windows the API creates it with an access list of its own service account, `BUILTIN\Administrators`
  and `SYSTEM`; anyone else gets access denied. Source-address checks are not used: proxies and containers
  make them unreliable.
- Every request is sealed with the host's Data Protection file ring (`Wms:DataProtection:KeyRingPath`), under
  its own purpose, and carries the time it was issued; the host refuses a request it cannot open or one more
  than two minutes old. A host that keeps its ring in the database (E8.6, containers without a mounted ring)
  has no file ring for the tool to use: mount one for both, or use `Wms:Auth:ForceLocal=true` as the recovery.
- The host records who ran the tool: the account the pipe reports on Windows, the tool's own OS user elsewhere.
  Every unlock and lock is a row of the audit trail with that name, and is logged at Warning.

## Running it

Windows service install: as the service account or a local administrator, from the install directory
(`appsettings.json` carries the ring path and the channel name):

```
wms-admin unlock --minutes 30
wms-admin status
wms-admin lock
```

Containers: `docker exec -it wms-api wms-admin unlock --minutes 30` inside the API container, with the file
ring mounted at the path the API uses.
