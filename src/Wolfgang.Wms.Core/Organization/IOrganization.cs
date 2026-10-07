// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Organization;

/// <summary>
/// The one organisation of the install (E16.0): created once by the first-run wizard, edited afterwards,
/// never deleted. Implemented over the database; before one exists every call answers
/// <c>organization.unavailable</c>.
/// </summary>
public interface IOrganization
{
    /// <summary>
    /// The organisation, or null while the first-run wizard has not created it.
    /// </summary>
    Task<OrganizationInfo?> GetAsync(CancellationToken cancellationToken);



    /// <summary>
    /// Creates the organisation; the install has exactly one.
    /// </summary>
    /// <exception cref="OrganizationException">It already exists, or the draft is invalid.</exception>
    Task<OrganizationInfo> CreateAsync(OrganizationDraft draft, string updatedBy, CancellationToken cancellationToken);



    /// <summary>
    /// Replaces every field of the organisation.
    /// </summary>
    /// <exception cref="OrganizationException">It has not been created, or the draft is invalid.</exception>
    Task<OrganizationInfo> UpdateAsync(OrganizationDraft draft, string updatedBy, CancellationToken cancellationToken);
}
