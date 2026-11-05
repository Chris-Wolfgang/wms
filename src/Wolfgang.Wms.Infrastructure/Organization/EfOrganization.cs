// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Organization;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.Infrastructure.Organization;

/// <summary>
/// <see cref="IOrganization"/> over <c>core.organization</c> (E16.0). The row is created once; every write
/// goes through the audited context (E6.4) and bumps the row version (E5.1).
/// </summary>
public sealed class EfOrganization : IOrganization
{
    private readonly WmsDbContext _context;
    private readonly TimeProvider _timeProvider;



    /// <summary>
    /// Creates the store.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfOrganization(WmsDbContext context, TimeProvider timeProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }



    /// <inheritdoc/>
    public async Task<OrganizationInfo?> GetAsync(CancellationToken cancellationToken)
    {
        var row = await _context.Organizations.AsNoTracking().OrderBy(o => o.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row?.ToInfo();
    }



    /// <inheritdoc/>
    public async Task<OrganizationInfo> CreateAsync(OrganizationDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Validate(draft);
        if (await _context.Organizations.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new OrganizationException(OrganizationErrorCodes.AlreadyExists, "The organization already exists; edit it instead of creating another.");
        }

        var row = new Organization();
        row.Apply(draft, _timeProvider.GetUtcNow(), updatedBy);
        await _context.Organizations.AddAsync(row, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.ToInfo();
    }



    /// <inheritdoc/>
    public async Task<OrganizationInfo> UpdateAsync(OrganizationDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Validate(draft);
        var row = await _context.Organizations.OrderBy(o => o.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new OrganizationException(OrganizationErrorCodes.NotCreated, "The organization has not been created yet; run the first-run wizard.");
        row.Apply(draft, _timeProvider.GetUtcNow(), updatedBy);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.ToInfo();
    }



    private static void Validate(OrganizationDraft draft)
    {
        if (OrganizationRules.Validate(draft) is { } reason)
        {
            throw new OrganizationException(OrganizationErrorCodes.Invalid, reason);
        }
    }
}
