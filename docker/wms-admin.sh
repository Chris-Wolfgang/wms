#!/bin/sh
# E9.3: wms-admin inside the API image (docs/ADMIN.md).
exec dotnet /opt/wms-admin/wms-admin.dll "$@"
