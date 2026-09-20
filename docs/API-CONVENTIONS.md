# API conventions (E82.3)

The rules every endpoint follows so an integrator learns them once. Versioning is in
[API-VERSIONING.md](API-VERSIONING.md); the helpers live in `Wolfgang.Wms.Core.Http`.

## Errors: problem details with codes

Every error is `application/problem+json` built from a typed `ErrorCode` (E1.13) through
`ApiProblems.Problem(code, detail, args)`: `status` is the code's HTTP status, `title` its message template
with the arguments applied, `type` the troubleshooting page at the code's anchor, and the extensions `code`
(`picking.tote_missing`), `severity` (`info`/`warning`/`error`) and `traceId`. Unhandled exceptions and empty
error statuses become problem details too (`UseWmsProblemDetails`). Handlers never invent a status code or a
message; they pick a code, and the catalog of codes is generated from the definitions.

## Status codes

| Code | When |
|------|------|
| `200` | read, update, or a replayed idempotent response |
| `201` + `Location` + body | create; the API never returns `3xx`, the console navigates |
| `202` | work handed to the outbox; the body says where to look |
| `204` | delete and body-less actions |
| `400` | malformed request (bad cursor, both `after` and `before`, invalid key) |
| `404` | not in the caller's site scope (hidden endpoints and flags too) |
| `409` | concurrency: `If-Match` did not match `row_version` |
| `422` | `Idempotency-Key` reused with a different body |

## Idempotency

`POST` and `PATCH` accept an optional `Idempotency-Key` header (1–128 visible ASCII characters,
`IdempotencyKey`). The API stores, per caller and key for 24 hours (`Idempotency.Retention`), the SHA-256
fingerprint of the accepted body and the response it sent (`IdempotencyRecord`, `IIdempotencyStore`). A retry
with the same key and body gets the stored response again; the same key with a different body gets `422`
(`Idempotency.Decide`). The check runs inside the handler's transaction so a race cannot create two side
effects. The console generates the key when a form opens and navigates to the created resource on success
(post-redirect-get); updates send `If-Match` with the `ETag` they read.

## Pagination: keyset, bidirectional

List endpoints take `[AsParameters] PageRequest`: `after` or `before` (opaque `Cursor`, never both), `size`
(default 50, capped at 500), and `id_from`/`id_to` so parallel clients can split a table. They return
`Page<T>`: `items` in the endpoint's fixed indexed sort, `next_cursor`/`previous_cursor`, the exact
`total_count`, and `min_id`/`max_id` for the scope. Cursors are stable and may appear in shareable console
URLs; filters always live in the URL; console grids are virtualised (scroll-loaded, `focus=<id>` centres on a
row), no page numbers. Endpoints over unbounded tables (scan events, deposits, audit) require a time-range
filter and answer `400` without one.

## Compression

Responses are compressed with Brotli (gzip fallback) for JSON, XML, problem details and text
(`WmsCompression`), over TLS as well; the reverse proxy applies the ~1 KB threshold in production. Requests may
be gzip- or Brotli-compressed (`Content-Encoding`), so devices and imports can shrink uploads. Authentication
endpoints are marked `.DisableResponseCompression()` and are never compressed over TLS (BREACH). The file
connector can write `.gz` output, off by default.

## Content negotiation

Console and device clients use JSON (camelCase, source-generated). Endpoints used by external systems
(release intake, amendments, inventory feeds, confirmations and webhooks, master-data imports, connector
tests) will also accept and return XML selected by `Content-Type`/`Accept`, validated by XSDs generated from
the same models; that arrives with the first external endpoint (release intake), not before.

## Natural keys and time

URLs use natural keys where a customer would (`/skus/{skuCode}`), surrogate ids where they would not. Every
timestamp is UTC `DateTimeOffset` in ISO 8601; the client renders in the site's zone (E1.14).
