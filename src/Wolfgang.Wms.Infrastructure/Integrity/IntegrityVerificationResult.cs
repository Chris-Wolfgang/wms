// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Integrity;

/// <summary>
/// One run of the verification job (E10.4).
/// </summary>
/// <param name="RanAt">When the run finished (UTC).</param>
/// <param name="Checked">Signed rows verified.</param>
/// <param name="Failed">Rows whose signature did not match.</param>
public sealed record IntegrityVerificationResult(DateTimeOffset RanAt, int Checked, int Failed);
