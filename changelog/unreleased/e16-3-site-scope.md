type: feature

Site-scoped queries: `SiteScope` (the sites a caller holds a permission at) and the `InScope` query helper filter collection endpoints without a site in the route, starting with `GET /sites`, which a site-level grant may now call and sees only its own sites (E16.3).
