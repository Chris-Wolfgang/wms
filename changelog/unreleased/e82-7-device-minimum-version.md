type: feature

Device endpoints require the `X-Wms-Device-Version` header and answer `426 Upgrade Required` with the minimum version when the app is older than the site's minimum (`RequireDeviceVersion()`, `IDeviceVersionPolicy`).
