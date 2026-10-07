// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.BreakGlass.AdminChannel;

/// <summary>
/// <c>Wms:Admin</c> (E9.3): the host-only channel <c>wms-admin</c> talks to. A named pipe on Windows,
/// restricted to the service account, local administrators and SYSTEM; a Unix domain socket elsewhere. It is
/// a bootstrap key because the channel must exist before anyone can sign in to change a setting.
/// </summary>
public sealed class AdminChannelOptions
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public const string SectionName = "Wms:Admin";



    /// <summary>
    /// The configuration key of the pipe name, for messages.
    /// </summary>
    public const string NameKey = SectionName + ":ChannelName";



    /// <summary>
    /// The configuration key that turns the channel off.
    /// </summary>
    public const string EnabledKey = SectionName + ":Enabled";



    /// <summary>
    /// The pipe name; <see cref="AdminChannelProtocol.DefaultName"/> when not set. Two hosts on one machine
    /// (a lab) need different names.
    /// </summary>
    public string ChannelName { get; set; } = AdminChannelProtocol.DefaultName;



    /// <summary>
    /// False turns the channel off; <c>Wms:Auth:ForceLocal</c> is then the only way back in.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
