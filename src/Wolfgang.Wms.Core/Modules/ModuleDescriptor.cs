// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Routing;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Modules;

/// <summary>
/// Everything a module contributes to the host, declared once by the module's <c>Add…Module()</c> method
/// and applied by <see cref="WmsModuleEndpointRouteBuilderExtensions.MapWmsModules"/>. Registration is
/// explicit: there is no assembly scanning, and every contribution is a typed key (E1.13), never a string.
/// </summary>
/// <remarks>
/// Navigation, EF configurations and resources join this record with the stories that define their shapes
/// (E82.4, E2, E1.14).
/// </remarks>
/// <param name="Name">Stable module name, unique within a host, for example <c>Picking</c>.</param>
/// <param name="EndpointMappers">Callbacks that map the module's endpoints onto the host's route builder, in order.</param>
/// <param name="Jobs">Worker jobs the module owns.</param>
/// <param name="Settings">Settings the module defines, of every value type.</param>
/// <param name="Permissions">Permissions the module's endpoints check.</param>
/// <param name="FeatureFlags">Feature flags the module consults at its edges.</param>
/// <param name="LicenseFeatures">License features the module's capabilities sit behind.</param>
/// <param name="IssueTypes">Issue types the module raises.</param>
/// <param name="ErrorCodes">Error codes the module returns.</param>
public sealed record ModuleDescriptor
(
    string Name,
    IReadOnlyList<Action<IEndpointRouteBuilder>> EndpointMappers,
    IReadOnlyList<JobName> Jobs,
    IReadOnlyList<SettingKey> Settings,
    IReadOnlyList<Permission> Permissions,
    IReadOnlyList<FeatureFlag> FeatureFlags,
    IReadOnlyList<LicenseFeature> LicenseFeatures,
    IReadOnlyList<IssueType> IssueTypes,
    IReadOnlyList<ErrorCode> ErrorCodes
)
{
    /// <summary>
    /// A descriptor with no contributions yet; add them with the fluent <c>With…</c> methods.
    /// </summary>
    public static ModuleDescriptor Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new ModuleDescriptor(name, [], [], [], [], [], [], [], []);
    }



    /// <summary>
    /// Adds an endpoint mapper. Mappers run in the order they were added, once, when the host maps modules.
    /// </summary>
    public ModuleDescriptor WithEndpoints(Action<IEndpointRouteBuilder> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return this with { EndpointMappers = [.. EndpointMappers, map] };
    }



    /// <summary>Adds the module's job names.</summary>
    public ModuleDescriptor WithJobs(params IReadOnlyList<JobName> jobs)
    {
        return this with { Jobs = Append(Jobs, jobs) };
    }



    /// <summary>Adds the module's settings.</summary>
    public ModuleDescriptor WithSettings(params IReadOnlyList<SettingKey> settings)
    {
        return this with { Settings = Append(Settings, settings) };
    }



    /// <summary>Adds the module's permissions.</summary>
    public ModuleDescriptor WithPermissions(params IReadOnlyList<Permission> permissions)
    {
        return this with { Permissions = Append(Permissions, permissions) };
    }



    /// <summary>Adds the module's feature flags.</summary>
    public ModuleDescriptor WithFeatureFlags(params IReadOnlyList<FeatureFlag> flags)
    {
        return this with { FeatureFlags = Append(FeatureFlags, flags) };
    }



    /// <summary>Adds the module's license features.</summary>
    public ModuleDescriptor WithLicenseFeatures(params IReadOnlyList<LicenseFeature> features)
    {
        return this with { LicenseFeatures = Append(LicenseFeatures, features) };
    }



    /// <summary>Adds the module's issue types.</summary>
    public ModuleDescriptor WithIssueTypes(params IReadOnlyList<IssueType> issueTypes)
    {
        return this with { IssueTypes = Append(IssueTypes, issueTypes) };
    }



    /// <summary>Adds the module's error codes.</summary>
    public ModuleDescriptor WithErrorCodes(params IReadOnlyList<ErrorCode> errorCodes)
    {
        return this with { ErrorCodes = Append(ErrorCodes, errorCodes) };
    }



    private static IReadOnlyList<T> Append<T>(IReadOnlyList<T> existing, IReadOnlyList<T> added)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(added);
        if (added.Any(item => item is null))
        {
            throw new ArgumentException("Contributions must not contain null.", nameof(added));
        }

        return [.. existing, .. added];
    }
}
