// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Identifiers;

/// <summary>
/// One application identifier and its value from a GS1 element string.
/// </summary>
/// <param name="ApplicationIdentifier">The AI.</param>
/// <param name="Value">The value exactly as encoded.</param>
public sealed record Gs1Element(Gs1ApplicationIdentifier ApplicationIdentifier, string Value)
{
    /// <summary>
    /// The human-readable form: <c>(01)09501101530003</c>.
    /// </summary>
    public override string ToString()
    {
        return "(" + ApplicationIdentifier.Code + ")" + Value;
    }
}
