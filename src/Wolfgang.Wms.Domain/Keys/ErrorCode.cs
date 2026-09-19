// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// A stable, documented error identifier. Defined once as a <c>static readonly</c> instance in a definitions
/// class; the catalog is enumerated to generate the troubleshooting reference (E1.13).
/// </summary>
/// <param name="Code">Stable code, for example <c>picking.tote_already_closed</c>.</param>
/// <param name="HttpStatus">HTTP status the API answers with when this error is the outcome (100–599).</param>
/// <param name="MessageTemplate">User-facing message; may contain <c>{placeholders}</c> filled by the caller.</param>
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
    public string MessageTemplate { get; } = RequireText(MessageTemplate, nameof(MessageTemplate));



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
}
