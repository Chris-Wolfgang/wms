// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Organization;

/// <summary>
/// The organisation on a host without a database (E16.0): nothing is stored, so every call answers
/// <c>organization.unavailable</c>. Replaced by the stored one when <c>AddWmsDatabase</c> runs.
/// </summary>
public sealed class NoOrganization : IOrganization
{
    /// <inheritdoc/>
    /// <exception cref="OrganizationException">Always.</exception>
    public Task<OrganizationInfo?> GetAsync(CancellationToken cancellationToken)
    {
        throw Unavailable();
    }



    /// <inheritdoc/>
    /// <exception cref="OrganizationException">Always.</exception>
    public Task<OrganizationInfo> CreateAsync(OrganizationDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        throw Unavailable();
    }



    /// <inheritdoc/>
    /// <exception cref="OrganizationException">Always.</exception>
    public Task<OrganizationInfo> UpdateAsync(OrganizationDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        throw Unavailable();
    }



    private static OrganizationException Unavailable()
    {
        return new OrganizationException(OrganizationErrorCodes.Unavailable, "The database is not configured; there is no organization to read or write.");
    }
}
