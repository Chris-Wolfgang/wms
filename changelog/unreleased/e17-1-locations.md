type: feature

Locations: each bin has a code and a barcode unique within the site, a zone, a sortable walk sequence that starts with the zone's walk-order prefix, a pickable flag and an active flag, created and edited through `GET`/`POST`/`PUT /sites/{siteId}/locations` with `If-Match`; the list is the first keyset-paged endpoint (`after`/`before` cursors, `sort`, `size`, `id_from`/`id_to`, `zone_id`) (E17.1).
