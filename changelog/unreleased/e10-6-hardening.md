type: feature

Hardening: `X-Forwarded-*` honoured only when `Wms:Hosting:BehindProxy` says a reverse proxy fronts the hosts; browser origins allowed by the `api.cors.allowed_origins` setting without a restart (credentials and `ETag` for listed origins, none otherwise); the console sends a Blazor-tuned content-security policy, `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options` and a minimal `Permissions-Policy` on every response.
