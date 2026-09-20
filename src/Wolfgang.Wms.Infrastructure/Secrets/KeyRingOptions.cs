// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Infrastructure.Secrets;

/// <summary>
/// <c>Wms:DataProtection</c> (E8.1): where the Data Protection key ring lives. With a path the ring is a
/// directory of key files the installer backs up and mounts into containers (created on first run with
/// permissions for the running user only); without one the ring is stored in the database (E8.6), which
/// every instance shares. An encrypted connection string (E8.2) needs the file ring: the database cannot be
/// opened before it is decrypted.
/// </summary>
public sealed class KeyRingOptions
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public const string SectionName = "Wms:DataProtection";



    /// <summary>
    /// The configuration key of the path, for messages.
    /// </summary>
    public const string PathKey = SectionName + ":KeyRingPath";



    /// <summary>
    /// The key ring directory, or null for the database-backed ring.
    /// </summary>
    public string? KeyRingPath { get; set; }



    /// <summary>
    /// True when a directory is configured.
    /// </summary>
    public bool UsesFileSystem => !string.IsNullOrWhiteSpace(KeyRingPath);
}
