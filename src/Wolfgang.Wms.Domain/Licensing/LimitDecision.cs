// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Domain.Licensing;

/// <summary>
/// The decision for one creation (E79.4): the outcome, the counts and, while in grace, how long is left.
/// </summary>
/// <param name="Outcome">What may happen.</param>
/// <param name="Limit">The limit.</param>
/// <param name="Ceiling">The licensed value.</param>
/// <param name="Count">The count after the creation.</param>
/// <param name="GraceEndsOn">The last day of grace while over the limit, else null.</param>
/// <param name="Message">The message shown: the banner while within the allowance, the refusal when blocked.</param>
public sealed record LimitDecision(LimitOutcome Outcome, LicenseLimit Limit, LimitValue Ceiling, int Count, DateOnly? GraceEndsOn, string Message);
