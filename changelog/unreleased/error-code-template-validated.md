type: fix

`ErrorCode` validates its message template at construction: only positional `{0}` placeholders and `{{` / `}}` escapes are accepted, so a named or malformed placeholder fails at startup instead of turning the error response into a 500 the first time an argument is passed.
