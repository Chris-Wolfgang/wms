type: docs

The API conventions say what the code answers to a conditional update: `412` when `If-Match` is stale (or weak), `428` when it is missing or `*`; the old `409` line was never what the API returned.
