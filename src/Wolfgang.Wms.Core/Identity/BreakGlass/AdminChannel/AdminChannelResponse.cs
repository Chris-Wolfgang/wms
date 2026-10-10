// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.BreakGlass.AdminChannel;

/// <summary>
/// The host's answer on the admin channel (E9.3): whether the command ran, a line for the operator, and the
/// gate's status afterwards when the host could read it.
/// </summary>
/// <param name="Ok">True when the command ran.</param>
/// <param name="Message">What happened, for the operator.</param>
/// <param name="Status">The gate after the command; null when the request was refused before reaching it.</param>
public sealed record AdminChannelResponse(bool Ok, string Message, LocalLoginStatus? Status);
