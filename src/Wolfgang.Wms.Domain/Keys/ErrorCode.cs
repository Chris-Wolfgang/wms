// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// A stable, documented error identifier. Defined once as a <c>static readonly</c> instance in a definitions
/// class; the catalog is enumerated to generate the troubleshooting reference (E1.13).
/// </summary>
/// <param name="Code">Stable code, for example <c>picking.tote_already_closed</c>.</param>
/// <param name="HttpStatus">HTTP status the API answers with when this error is the outcome (100–599).</param>
/// <param name="MessageTemplate">User-facing message; may contain positional placeholders <c>{0}</c>, <c>{1}</c>…
/// (with an optional <c>,alignment</c> or <c>:format</c>, as <see cref="string.Format(IFormatProvider, string, object[])"/>
/// reads them) that the caller fills, and <c>{{</c> / <c>}}</c> for literal braces. Anything else between braces,
/// such as a named <c>{tote}</c>, is refused here, so a bad template fails at startup instead of turning the
/// error response into a 500 the first time an argument is passed.</param>
/// <param name="DocsAnchor">Anchor in the troubleshooting reference, for example <c>tote-already-closed</c>.</param>
/// <param name="Severity">How the error is surfaced.</param>
public sealed record ErrorCode
(
    string Code,
    int HttpStatus,
    string MessageTemplate,
    string DocsAnchor,
    ErrorSeverity Severity
)
{
    /// <summary>
    /// Stable code.
    /// </summary>
    public string Code { get; } = KeyName.Require(Code, nameof(Code));



    /// <summary>
    /// HTTP status the API answers with.
    /// </summary>
    public int HttpStatus { get; } = HttpStatus is >= 100 and <= 599
        ? HttpStatus
        : throw new ArgumentOutOfRangeException(nameof(HttpStatus), HttpStatus, "HTTP status must be between 100 and 599.");



    /// <summary>
    /// User-facing message template.
    /// </summary>
    public string MessageTemplate { get; } = RequireTemplate(MessageTemplate, nameof(MessageTemplate));



    /// <summary>
    /// Anchor in the troubleshooting reference.
    /// </summary>
    public string DocsAnchor { get; } = RequireText(DocsAnchor, nameof(DocsAnchor));



    private static string RequireText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        return value;
    }



    /// <summary>
    /// A non-empty template whose braces are all <c>{{</c>, <c>}}</c> or a positional placeholder
    /// <c>{index[,alignment][:format]}</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The template is empty, or has a brace that is neither an escape nor a
    /// positional placeholder.</exception>
    private static string RequireTemplate(string value, string paramName)
    {
        RequireText(value, paramName);
        var i = 0;
        while (i < value.Length)
        {
            var c = value[i];
            if (c == '}')
            {
                if (i + 1 >= value.Length || value[i + 1] != '}')
                {
                    throw new ArgumentException($"The message template has a stray '}}' at position {i}; write '}}}}' for a literal brace.", paramName);
                }

                i += 2;
            }
            else if (c == '{')
            {
                i = i + 1 < value.Length && value[i + 1] == '{' ? i + 2 : SkipPlaceholder(value, i, paramName);
            }
            else
            {
                i++;
            }
        }

        return value;
    }



    private static int SkipPlaceholder(string value, int open, string paramName)
    {
        var i = open + 1;
        var digits = 0;
        while (i < value.Length && char.IsAsciiDigit(value[i]))
        {
            i++;
            digits++;
        }

        var close = value.IndexOf('}', i);
        var wellFormed = digits > 0 && close >= 0 && (close == i || value[i] == ',' || value[i] == ':');
        if (!wellFormed)
        {
            throw new ArgumentException($"The message template has an invalid placeholder at position {open}; use positional '{{0}}', '{{1}}'… (optionally '{{0,5}}' or '{{0:x}}') and '{{{{' / '}}}}' for literal braces.", paramName);
        }

        return close + 1;
    }
}
