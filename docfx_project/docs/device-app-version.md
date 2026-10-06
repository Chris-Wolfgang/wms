# Device app version

Every call the handheld app makes to the server says which version of the app is calling. The server can
then require a minimum version, so an administrator can retire an old app build safely.

## The header

The app sends its version in the `X-Wms-Device-Version` header on every call to a device endpoint, for
example:

```
X-Wms-Device-Version: 1.4.2
```

Only the numeric part counts: `1.4.2-beta+5` is read as `1.4.2`. Versions compare by major, minor and build;
a missing build counts as zero (`1.4` is the same as `1.4.0`), and a fourth part is ignored.

## What the server answers

| Situation | Status | Error code |
|-----------|--------|------------|
| The header is missing | 400 Bad Request | `device.version_missing` |
| The header is not a version | 400 Bad Request | `device.version_invalid` |
| The app is older than the minimum | 426 Upgrade Required | `device.version_too_old` |

Each answer is a standard problem response (`application/problem+json`) with the error `code`. The 426 answer
also carries the minimum in a `minimumVersion` field, so the device can tell the user which version to install
(abbreviated):

```json
{
  "status": 426,
  "title": "App version 1.3.0 is below the minimum 1.4.0; update the app.",
  "code": "device.version_too_old",
  "severity": "warning",
  "minimumVersion": "1.4.0"
}
```

## Setting the minimum

The minimum will be a setting that cascades from the organisation to each site, so a new app version can be
rolled out one warehouse at a time while the server runs one version for all. Until that setting exists, no
minimum is configured and every app version is accepted.
