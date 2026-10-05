// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Identifiers;

/// <summary>
/// The extra structural check a GS1 application identifier's value must pass beyond length and digits.
/// </summary>
public enum Gs1ValueRule
{
    /// <summary>
    /// Length and character class only.
    /// </summary>
    None = 0,

    /// <summary>
    /// The last digit is a GS1 mod-10 check digit (GTIN, SSCC, GLN, GSIN, GSRN).
    /// </summary>
    CheckDigit = 1,

    /// <summary>
    /// <c>YYMMDD</c>; a day of <c>00</c> means the last day of the month.
    /// </summary>
    Date = 2,

    /// <summary>
    /// <c>YYMMDDhhmm</c> (AI 7003): a real calendar date (no day <c>00</c>), hour <c>00</c>–<c>23</c>, minute
    /// <c>00</c>–<c>59</c>.
    /// </summary>
    DateTime = 3,

    /// <summary>
    /// A three-digit ISO 3166-1 numeric country code followed by 1 to 9 characters (AI 421, ship-to postal code
    /// with country).
    /// </summary>
    CountryAndPostalCode = 4,
}
