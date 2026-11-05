// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.BreakGlass.AdminChannel;

/// <summary>
/// One command sent over the host-only admin channel (E9.3): what to do with the break-glass gate, by whom
/// (the OS user running <c>wms-admin</c>), and when it was issued, so a captured message cannot be replayed
/// later. The whole record travels sealed with the host's Data Protection key.
/// </summary>
/// <param name="Command">One of <see cref="AdminChannelCommands"/>.</param>
/// <param name="Minutes">For <see cref="AdminChannelCommands.Unlock"/>: the window length; null for the default.</param>
/// <param name="OsUser">The operating-system user running the tool, as the tool sees it.</param>
/// <param name="IssuedAt">When the tool built the request; the host refuses one older than the allowed skew.</param>
public sealed record AdminChannelRequest(string Command, int? Minutes, string OsUser, DateTimeOffset IssuedAt);
