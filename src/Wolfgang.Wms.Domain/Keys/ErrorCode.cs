// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text;

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// A stable, documented error identifier. Defined once as a <c>static readonly</c> instance in a definitions
/// class; the catalog is enumerated to generate the troubleshooting reference (E1.13).
/// </summary>
/// <param name="Code">Stable code, for example <c>picking.tote_already_closed</c>.</param>
/// <param name="HttpStatus">HTTP status the API answers with when this error is the outcome (100–599).</param>
/// <param name="MessageTemplate">User-facing message; may contain positional placeholders <c>{0}</c>, <c>{1}</c>…
/// (with an optional <c>,alignment</c> or <c>:format</c>, as <see cref="string.Format(IFormatProvider, string, object[])"/>
/// reads them) that the caller fills, and <c>{{</c> / <c>}}</c> for literal braces. The template is checked with
/// <see cref="CompositeFormat.Parse(string)"/>, the parser <c>string.Format</c> itself uses, so anything it would
/// throw on, such as a named <c>{tote}</c> or an empty alignment <c>{0,}</c>, is refused here and a bad template
/// fails at startup instead of turning the error response into a 500 the first time it is formatted.</param>
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
    /// A non-empty template that <see cref="string.Format(IFormatProvider, string, object[])"/> accepts: positional
    /// placeholders <c>{index[,alignment][:format]}</c> and <c>{{</c> / <c>}}</c> escapes, checked by the same parser
    /// (<see cref="CompositeFormat.Parse(string)"/>), so nothing that passes here throws when it is formatted.
    /// </summary>
    /// <exception cref="ArgumentException">The template is empty or not a valid composite format; the inner
    /// exception says where the parser stopped.</exception>
    private static string RequireTemplate(string value, string paramName)
    {
        RequireText(value, paramName);
        try
        {
            CompositeFormat.Parse(value);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The message template is not a valid composite format: " + exception.Message + " Use positional '{0}', '{1}'… (optionally '{0,5}' or '{0:x}') and '{{' / '}}' for literal braces.", paramName, exception);
        }

        return value;
    }
}
