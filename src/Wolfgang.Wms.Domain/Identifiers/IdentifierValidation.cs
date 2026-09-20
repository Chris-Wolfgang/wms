// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Identifiers;

/// <summary>
/// The outcome of validating one identifier against its profile (E3.7): the normalised value on success, or
/// the field, the failed rule and the expected format on failure, so every surface (intake, scan, console,
/// import, API) reports the same message.
/// </summary>
/// <param name="Field">The field that was validated.</param>
/// <param name="Value">The normalised value; null on failure.</param>
/// <param name="FailedRule">The rule that failed (<c>required</c>, <c>control_characters</c>, <c>min_length</c>, <c>max_length</c>, <c>format</c>); null on success.</param>
/// <param name="Expected">What the rule expected, in words a user can act on; null on success.</param>
public sealed record IdentifierValidation(string Field, string? Value, string? FailedRule, string? Expected)
{
    /// <summary>
    /// True when the value passed every rule.
    /// </summary>
    public bool IsValid => FailedRule is null;



    /// <summary>
    /// A passing result.
    /// </summary>
    public static IdentifierValidation Success(string field, string value)
    {
        return new IdentifierValidation(field, value, FailedRule: null, Expected: null);
    }



    /// <summary>
    /// A failing result naming the rule and the expectation.
    /// </summary>
    public static IdentifierValidation Failure(string field, string rule, string expected)
    {
        return new IdentifierValidation(field, Value: null, rule, expected);
    }



    /// <summary>
    /// The message shown to a user: "<c>field</c>: expected …", or empty on success.
    /// </summary>
    public string Message => IsValid ? string.Empty : $"{Field}: {Expected}";
}
