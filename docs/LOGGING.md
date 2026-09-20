# Logging (E12.2, E12.3, E12.4)

Every host (API, console, worker) logs through Serilog as JSON lines: one event per line, compact format,
structured properties, no colours. Where the lines go is a bootstrap key; what level is logged is a
setting, changed without a restart.

## Sinks (`Wms:Logging`)

| Key | Default | Meaning |
|---|---|---|
| `Wms:Logging:Level` | `Information` | The level the host boots with, until the settings take over |
| `Wms:Logging:Stdout` | on unless a file or the Event Log is configured | Plain JSON to standard output: the container log pipe (`docker logs -f`, the platform's collector). Asynchronous; never a watched console |
| `Wms:Logging:File:Path` | none | A rolling file (`wms-.log` becomes `wms-20260920.log`), one per day, for a Windows service or IIS install, or a volume in a container |
| `Wms:Logging:File:RetainedDays` | `14` | How many daily files to keep |
| `Wms:Logging:EventLog:Source` | none | The Windows Event Log source; Warning and above only (what an operator must see). Ignored elsewhere |
| `Wms:Logging:OpenTelemetry:Endpoint` | none | The OTLP endpoint of the customer's collector; every other destination (Seq, Loki, Elasticsearch, Splunk, Datadog, Application Insights, CloudWatch, syslog) is reached through it, never through a sink per vendor |
| `Wms:Logging:OpenTelemetry:Protocol` | `grpc` | `grpc` or `http` (HTTP/protobuf) |
| `Wms:Logging:VerbosePerSecond` | `200` | Verbose lines and SQL command lines allowed per second; the rest of that second is dropped |

There is no database sink: application logs stay out of the business database. Live viewing is
`docker logs -f` or the file, plus a timed level elevation (below).

## Levels

| Level | Use it for | Examples |
|---|---|---|
| Verbose | per-scan and per-payload detail | every barcode read, request and response bodies, SQL parameters |
| Debug | decisions and rule outcomes | why a tote was routed here, which setting value won, cache hits |
| Information | business milestones | order released, tote closed, user signed in, job finished |
| Warning | recovered problems | retry succeeded, a stale value was ignored, a provider was slow |
| Error | failed operations that need attention | a save failed, a sign-in failed at the provider, a job run failed |
| Fatal | the process cannot continue | the database is unreachable at start, the key ring is missing |

Rules: use `[LoggerMessage]` source-generated methods (a message template, named properties, the level
declared at the call site); never string interpolation into a log call; never `Console.Write` in a host or
library (the migrate and simulator command-line tools are the exception). `scripts/Check-LogLevels.ps1` enforces
these at the PR gate.

## Redaction

Every event passes the redacting enricher before any sink: a property whose name says it is a secret
(`password`, `secret`, `connectionstring`, `pin`, `licensekey`, `token`, `apikey`, `authorization`)
becomes `***`; inside any string property an `enc:v1:` payload, a connection-string password and a bearer
token are masked. Exception messages are not rewritten: never put a secret in one.

## Correlation (E12.3)

While a request runs, every line carries `TraceId`, `UserId` and `User` (when signed in), `Device` (the
`X-Wms-Device` header) and the business identifiers in the route (`ToteId`, `ReleaseId`, `DepositRunId`,
`RunId`, `PickerId`, `SiteId`, `DeviceId`, `UserId`, `RoleId`) as properties, so one tote's or one run's
lines can be pulled from any sink. Worker jobs push the same properties for the entity they work on.

## Runtime level (E12.4)

| Setting | Default | Meaning |
|---|---|---|
| `logging.level` | `Information` | The server's minimum level |
| `logging.elevated_level` | `Information` | The level while an elevation runs (Trace, Debug or Information) |
| `logging.elevated_until` | in the past | When the elevation ends |
| `logging.elevation_max_minutes` | `120` | The longest elevation the console may start |

One level switch per host follows these settings within 5 seconds (every instance, every host).
Elevation is timed only: `POST /system/logging/elevate {"level":"Debug","minutes":30}` lowers the level
and reverts by itself; `DELETE /system/logging/elevate` ends it now; `GET /system/logging` shows the
permanent level, the level in force and the running elevation (`logging.manage`; Supervisor and Support
hold it). There is no permanent elevation from the API; a permanent change is the `logging.level`
setting (`settings.write`). The console shows a banner while elevated (Configure workspace, E82).

Pushing a level to a device (or every device at a site), picker-started debug windows and device log
upload are the paid feature `devices.remote_logging` and arrive with the device stories.
