type: fix

`ErrorCode` validates its message template's syntax at construction with the framework's composite-format parser, so a malformed template (a named `{tote}`, an empty alignment `{0,}`) fails at startup instead of turning the error response into a 500 the first time it is formatted; missing arguments and type-specific formats such as `{0:Q}` are still only caught when the title is formatted. `ApiProblems.Title` always formats the template, so `{{` / `}}` render as braces even when there are no arguments.
