type: fix

An endpoint marked `DisableResponseCompression` (authentication) sends its body uncompressed on plain HTTP as well as HTTPS, so the BREACH protection holds when TLS ends at a reverse proxy; before, the opt-out applied only when Kestrel itself saw HTTPS.
