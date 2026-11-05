type: fix

`PageQuery` carries the validated `id_from` / `id_to` bounds (and `HasIdRange`), so an endpoint can honour the range a parallel client asked for instead of the request validating it and dropping it.
