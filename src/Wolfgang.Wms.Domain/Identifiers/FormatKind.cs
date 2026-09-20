// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Identifiers;

/// <summary>
/// How an identifier format is written (E3.8): a simple mask or a full .NET regular expression. Masks compile
/// to a regular expression, so one engine validates both.
/// </summary>
public enum FormatKind
{
    /// <summary>
    /// Mask syntax: <c>A</c> letter, <c>9</c> digit, <c>X</c> alphanumeric, <c>?</c> any character,
    /// <c>(n)</c> repeat count, everything else literal (<c>AAA-999</c>, <c>9(8)</c>, <c>T-X(6)</c>).
    /// </summary>
    Mask = 0,

    /// <summary>
    /// .NET regular expression syntax, anchored to the whole value.
    /// </summary>
    Regex = 1,
}
