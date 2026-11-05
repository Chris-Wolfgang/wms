// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Wolfgang.Wms.Core.Secrets;

namespace Wolfgang.Wms.Infrastructure.Secrets;

/// <summary>
/// The file-system key ring (E8.1): the directory is created on first run with permissions for the running
/// user only, and tools that run outside a host (the migrate tool) open the same ring to decrypt the
/// connection string.
/// </summary>
public static class KeyRing
{
    /// <summary>
    /// The application name every host and tool uses, so they share one ring.
    /// </summary>
    public const string ApplicationName = "Wolfgang.Wms";



    /// <summary>
    /// The purpose string of the secrets protector; changing it would orphan every stored secret.
    /// </summary>
    public const string Purpose = "Wolfgang.Wms.Secrets.v1";



    /// <summary>
    /// Ensures the key ring directory exists, creating it with access for the current user only.
    /// </summary>
    /// <returns>The directory.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null or blank.</exception>
    public static DirectoryInfo EnsureDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = new DirectoryInfo(Path.GetFullPath(path));
        if (directory.Exists)
        {
            return directory;
        }

        if (OperatingSystem.IsWindows())
        {
            CreateForCurrentUserOnWindows(directory);
        }
        else
        {
            Directory.CreateDirectory(directory.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        directory.Refresh();
        return directory;
    }



    /// <summary>
    /// A protector over the file ring at <paramref name="path"/> for use outside a host (the migrate tool,
    /// the installer): the same application name and purpose as the hosts.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null or blank.</exception>
    public static ISecretProtector CreateProtector(string path)
    {
        var directory = EnsureDirectory(path);
        var provider = DataProtectionProvider.Create(directory, options => options.SetApplicationName(ApplicationName));
        return new DataProtectionSecretProtector(provider);
    }



    /// <summary>
    /// The protector for the ring named by <c>Wms:DataProtection:KeyRingPath</c> in
    /// <paramref name="configuration"/>, or null when no path is configured.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
    public static ISecretProtector? TryCreateProtector(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var path = configuration[KeyRingOptions.PathKey];
        return string.IsNullOrWhiteSpace(path) ? null : CreateProtector(path);
    }



    [SupportedOSPlatform("windows")]
    private static void CreateForCurrentUserOnWindows(DirectoryInfo directory)
    {
        var user = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("The current Windows identity has no security identifier.");
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        directory.Create(security);
    }
}
