// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Database.Auditing;

namespace Wolfgang.Wms.Infrastructure.Identity;

/// <summary>
/// <see cref="IRoles"/> over <c>core.role</c>, <c>core.role_permission</c> and <c>core.user_role</c>
/// (E10.2, E10.3). Every write goes through the audited context.
/// </summary>
public sealed class EfRoles : IRoles
{
    private const string Wildcard = PermissionClaims.Wildcard;
    private readonly WmsDbContext _context;
    private readonly PermissionCatalog _catalog;
    private readonly TimeProvider _timeProvider;



    /// <summary>
    /// Creates the roles.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public EfRoles(WmsDbContext context, PermissionCatalog catalog, TimeProvider timeProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }



    /// <inheritdoc/>
    public async Task<IReadOnlyList<RoleInfo>> ListAsync(CancellationToken cancellationToken)
    {
        var roles = await _context.Set<Role>().Include(r => r.Permissions).AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var builtInOrder = BuiltInRoles.All.Select(r => r.Key()).ToList();
        return roles
            .OrderBy(r => r.BuiltInKey is null ? builtInOrder.Count : builtInOrder.IndexOf(r.BuiltInKey))
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(View)
            .ToList();
    }



    /// <inheritdoc/>
    public async Task<RoleInfo> FindAsync(long roleId, CancellationToken cancellationToken)
    {
        return View(await RequireRoleAsync(roleId, track: false, cancellationToken).ConfigureAwait(false));
    }



    /// <inheritdoc/>
    public async Task<RoleInfo> CreateAsync(RoleDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Validate(draft);
        await RequireNameFreeAsync(draft.Name, exceptRoleId: null, cancellationToken).ConfigureAwait(false);
        var role = new Role { Name = draft.Name.Trim(), NameNormalized = Normalize(draft.Name), Description = draft.Description.Trim(), UpdatedBy = updatedBy, UpdatedAt = _timeProvider.GetUtcNow() };
        Replace(role, draft.Permissions);
        _context.Set<Role>().Add(role);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return View(role);
    }



    /// <inheritdoc/>
    public async Task<RoleInfo> UpdateAsync(long roleId, RoleDraft draft, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        var role = await RequireRoleAsync(roleId, track: true, cancellationToken).ConfigureAwait(false);
        RequireEditable(role);
        Validate(draft);
        await RequireNameFreeAsync(draft.Name, roleId, cancellationToken).ConfigureAwait(false);
        role.Name = draft.Name.Trim();
        role.NameNormalized = Normalize(draft.Name);
        role.Description = draft.Description.Trim();
        role.UpdatedBy = updatedBy;
        role.UpdatedAt = _timeProvider.GetUtcNow();
        Replace(role, draft.Permissions);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return View(role);
    }



    /// <inheritdoc/>
    public async Task<RoleInfo> CopyAsync(long roleId, string newName, string updatedBy, CancellationToken cancellationToken)
    {
        var source = await RequireRoleAsync(roleId, track: false, cancellationToken).ConfigureAwait(false);
        var permissions = source.Permissions.Select(p => p.PermissionName).Where(p => !string.Equals(p, Wildcard, StringComparison.Ordinal)).ToList();
        if (source.Permissions.Any(p => string.Equals(p.PermissionName, Wildcard, StringComparison.Ordinal)))
        {
            permissions = _catalog.All.Select(p => p.Name).ToList();   // a copy of the administrator lists every permission explicitly, so it can be trimmed
        }

        return await CreateAsync(new RoleDraft(newName ?? string.Empty, source.Description, permissions), updatedBy, cancellationToken).ConfigureAwait(false);
    }



    /// <inheritdoc/>
    public async Task DeleteAsync(long roleId, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        var role = await RequireRoleAsync(roleId, track: true, cancellationToken).ConfigureAwait(false);
        RequireEditable(role);
        _context.Set<UserRole>().RemoveRange(await _context.Set<UserRole>().Where(a => a.RoleId == roleId).ToListAsync(cancellationToken).ConfigureAwait(false));
        _context.Set<RolePermission>().RemoveRange(role.Permissions);
        _context.Set<Role>().Remove(role);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }



    /// <inheritdoc/>
    public async Task<IReadOnlyList<RoleAssignmentInfo>> AssignmentsOfAsync(long userId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var assignments = await _context.Set<UserRole>().Include(a => a.Role).AsNoTracking().Where(a => a.UserId == userId).OrderBy(a => a.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        return assignments.Select(a => View(a, now)).ToList();
    }



    /// <inheritdoc/>
    public async Task<RoleAssignmentInfo> AssignAsync(long userId, long roleId, long? siteId, DateTimeOffset? expiresAt, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        if (!await _context.Users.AnyAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false))
        {
            throw new AuthException(RoleErrorCodes.UserNotFound, $"User {userId} does not exist.");
        }

        var role = await RequireRoleAsync(roleId, track: false, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        var assignment = await _context.Set<UserRole>().SingleOrDefaultAsync(a => a.UserId == userId && a.RoleId == roleId && a.SiteId == siteId, cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            assignment = new UserRole { UserId = userId, RoleId = roleId, SiteId = siteId };
            _context.Set<UserRole>().Add(assignment);
        }

        assignment.ExpiresAt = expiresAt;
        assignment.UpdatedAt = now;
        assignment.UpdatedBy = updatedBy;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        assignment.Role = role;
        return View(assignment, now);
    }



    /// <inheritdoc/>
    public async Task UnassignAsync(long assignmentId, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        var assignment = await _context.Set<UserRole>().SingleOrDefaultAsync(a => a.Id == assignmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new AuthException(RoleErrorCodes.AssignmentNotFound, $"Assignment {assignmentId} does not exist.");
        _context.Set<UserRole>().Remove(assignment);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }



    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GrantsOfAsync(long userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var assignments = await _context.Set<UserRole>()
            .Include(a => a.Role!).ThenInclude(r => r.Permissions)
            .AsNoTracking()
            .Where(a => a.UserId == userId && (a.ExpiresAt == null || a.ExpiresAt > now))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return assignments
            .SelectMany(a => a.Role!.Permissions.Select(p => a.SiteId is { } site ? PermissionClaims.SiteGrant(p.PermissionName, site) : PermissionClaims.OrganizationGrant(p.PermissionName)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }



    /// <inheritdoc/>
    public async Task<int> EnsureBuiltInAsync(PermissionCatalog catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var existing = await _context.Set<Role>().Include(r => r.Permissions).Where(r => r.BuiltInKey != null).ToListAsync(cancellationToken).ConfigureAwait(false);
        var created = 0;
        var now = _timeProvider.GetUtcNow();
        foreach (var builtIn in BuiltInRoles.All)
        {
            var role = existing.SingleOrDefault(r => string.Equals(r.BuiltInKey, builtIn.Key(), StringComparison.Ordinal));
            if (role is null)
            {
                role = new Role { Name = builtIn.DisplayName(), NameNormalized = Normalize(builtIn.DisplayName()), Description = builtIn.Description(), BuiltInKey = builtIn.Key(), UpdatedBy = WmsAuditing.SystemIdentity, UpdatedAt = now };
                _context.Set<Role>().Add(role);
                created++;
            }

            var wanted = builtIn == BuiltInRole.Administrator
                ? [Wildcard]
                : catalog.All.Where(p => catalog.Find(p.Name)!.DefaultRoles.Contains(builtIn)).Select(p => p.Name).ToList();
            if (!wanted.Order(StringComparer.Ordinal).SequenceEqual(role.Permissions.Select(p => p.PermissionName).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                Replace(role, wanted, validate: false);
                role.UpdatedAt = now;
            }
        }

        await EnsureLocalAdministratorsHoldTheRoleAsync(now, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return created;
    }



    /// <summary>
    /// The normalised form of a role name (invariant upper case), the unique key.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Trim().ToUpperInvariant();
    }



    private async Task EnsureLocalAdministratorsHoldTheRoleAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var administrator = _context.Set<Role>().Local.Single(r => string.Equals(r.BuiltInKey, BuiltInRole.Administrator.Key(), StringComparison.Ordinal));
        var admins = await _context.Users.Where(u => u.IsLocalAdmin).Select(u => u.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var assigned = administrator.Id == 0
            ? []
            : await _context.Set<UserRole>().Where(a => a.RoleId == administrator.Id && a.SiteId == null).Select(a => a.UserId).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var userId in admins.Except(assigned))
        {
            _context.Set<UserRole>().Add(new UserRole { UserId = userId, Role = administrator, UpdatedAt = now, UpdatedBy = WmsAuditing.SystemIdentity });
        }
    }



    private void Replace(Role role, IReadOnlyList<string> permissions, bool validate = true)
    {
        var names = permissions.Select(p => p?.Trim() ?? string.Empty).Where(p => p.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (validate)
        {
            var unknown = names.FirstOrDefault(n => _catalog.Find(n) is null);
            if (unknown is not null)
            {
                throw new AuthException(RoleErrorCodes.UnknownPermission, $"'{unknown}' is not a permission in the catalog.");
            }
        }

        _context.Set<RolePermission>().RemoveRange(role.Permissions);
        role.Permissions.Clear();
        role.Permissions.AddRange(names.Select(n => new RolePermission { PermissionName = n }));
    }



    private async Task<Role> RequireRoleAsync(long roleId, bool track, CancellationToken cancellationToken)
    {
        var query = _context.Set<Role>().Include(r => r.Permissions);
        var role = await (track ? query : query.AsNoTracking()).SingleOrDefaultAsync(r => r.Id == roleId, cancellationToken).ConfigureAwait(false);
        return role ?? throw new AuthException(RoleErrorCodes.RoleNotFound, $"Role {roleId} does not exist.");
    }



    private async Task RequireNameFreeAsync(string name, long? exceptRoleId, CancellationToken cancellationToken)
    {
        var normalized = Normalize(name);
        if (await _context.Set<Role>().AnyAsync(r => r.NameNormalized == normalized && r.Id != exceptRoleId, cancellationToken).ConfigureAwait(false))
        {
            throw new AuthException(RoleErrorCodes.RoleNameTaken, $"A role named '{name.Trim()}' already exists.");
        }
    }



    private static void RequireEditable(Role role)
    {
        if (role.BuiltInKey is not null)
        {
            throw new AuthException(RoleErrorCodes.BuiltInRoleReadOnly, $"'{role.Name}' is a built-in role; copy it to make an editable one.");
        }
    }



    private static void Validate(RoleDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Name) || draft.Name.Trim().Length > Role.NameLength)
        {
            throw new AuthException(RoleErrorCodes.InvalidRole, $"A role needs a name of 1 to {Role.NameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(draft.Description) || draft.Description.Trim().Length > Role.DescriptionLength)
        {
            throw new AuthException(RoleErrorCodes.InvalidRole, $"A role needs a description of 1 to {Role.DescriptionLength} characters.");
        }

        if (draft.Permissions is null)
        {
            throw new AuthException(RoleErrorCodes.InvalidRole, "A role needs a list of permissions (it may be empty).");
        }
    }



    private static RoleInfo View(Role role)
    {
        return new RoleInfo(role.Id, role.Name, role.Description, role.BuiltInKey, role.Permissions.Select(p => p.PermissionName).Order(StringComparer.Ordinal).ToList(), role.RowVersion);
    }



    private static RoleAssignmentInfo View(UserRole assignment, DateTimeOffset now)
    {
        return new RoleAssignmentInfo(assignment.Id, assignment.UserId, assignment.RoleId, assignment.Role?.Name ?? string.Empty, assignment.SiteId, assignment.ExpiresAt, assignment.ExpiresAt is { } expiry && expiry <= now);
    }
}
