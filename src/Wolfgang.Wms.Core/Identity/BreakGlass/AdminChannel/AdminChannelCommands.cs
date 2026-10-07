// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Identity.BreakGlass.AdminChannel;

/// <summary>
/// The commands the channel accepts.
/// </summary>
public static class AdminChannelCommands
{
    /// <summary>Open local sign-in for a timed window.</summary>
    public const string Unlock = "unlock";

    /// <summary>Close an open window early.</summary>
    public const string Lock = "lock";

    /// <summary>Report the gate without changing it.</summary>
    public const string Status = "status";



    /// <summary>
    /// True for a command the channel knows.
    /// </summary>
    public static bool IsKnown(string? command)
    {
        return command is Unlock or Lock or Status;
    }
}
