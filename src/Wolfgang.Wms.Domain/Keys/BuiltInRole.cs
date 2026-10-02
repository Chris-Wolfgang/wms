// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Domain.Keys;

/// <summary>
/// The roles every installation starts with (E10.2). They are read-only; an administrator copies one to
/// edit it. Each permission names the built-in roles that hold it by default
/// (<see cref="Permission.DefaultRoles"/>), so a new module's permissions land in the right roles without
/// a migration; <see cref="Administrator"/> holds everything regardless.
/// </summary>
public enum BuiltInRole
{
    /// <summary>Everything, everywhere.</summary>
    Administrator,

    /// <summary>Runs the floor: live operations, resolution, reports.</summary>
    Supervisor,

    /// <summary>The resolution lane only: flagged totes, shorts, release to pack, tote inquiry, messages.</summary>
    Resolver,

    /// <summary>Reads settings and reports to help users; changes nothing.</summary>
    Support,

    /// <summary>Reads reports.</summary>
    Viewer,
}
