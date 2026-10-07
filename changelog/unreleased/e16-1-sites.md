type: feature

Sites: each warehouse has a unique code, a name, a time zone and an active flag, created and edited through `GET`/`POST`/`PUT /sites` with `If-Match`; retiring a site is refused while releases are open against it, and the settings cascade now runs over the stored sites (E16.1).
