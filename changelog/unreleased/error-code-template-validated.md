type: fix

`ErrorCode` validates its message template at construction with the framework's composite-format parser, so anything `string.Format` would throw on (a named `{tote}`, an empty alignment `{0,}`) fails at startup instead of turning the error response into a 500 the first time it is formatted. `ApiProblems.Title` always formats the template, so `{{` / `}}` render as braces even when there are no arguments.
