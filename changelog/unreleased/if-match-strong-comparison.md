type: fix

`If-Match` is checked with the strong comparison RFC 9110 requires: a `W/` weak tag no longer passes a conditional update (412), and the `*` wildcard answers 428 like a missing header, so optimistic concurrency cannot be skipped.
