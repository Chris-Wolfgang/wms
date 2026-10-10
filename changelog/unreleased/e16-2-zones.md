type: feature

Zones: each site has picking areas, bulk storage and resolution zones with a code unique within the site, a name, a type, an optional walk-order prefix, the reject-lane flag and, for resolution zones, the restocking bin, returns container, assigned resolvers, accepted exceptions and lane-or-queue kind, created and edited through `GET`/`POST`/`PUT /sites/{siteId}/zones` with `If-Match`; retiring a zone is refused while zone groups are open in it, and the settings cascade now runs organisation → site → zone over the stored rows (E16.2).
