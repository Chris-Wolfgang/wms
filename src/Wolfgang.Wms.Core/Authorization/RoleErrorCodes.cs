// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Authorization;

/// <summary>
/// Error codes of the <c>roles</c> module (E10.2, E10.3).
/// </summary>
public static class RoleErrorCodes
{
    /// <summary>No role with that id.</summary>
    public static ErrorCode RoleNotFound { get; } = new("auth.role_not_found", StatusCodes.Status404NotFound, "Role {0} does not exist.", "auth-role-not-found", ErrorSeverity.Error);

    /// <summary>Another role already has that name.</summary>
    public static ErrorCode RoleNameTaken { get; } = new("auth.role_name_taken", StatusCodes.Status409Conflict, "A role named '{0}' already exists.", "auth-role-name-taken", ErrorSeverity.Error);

    /// <summary>Built-in roles are read-only; copy one to edit it.</summary>
    public static ErrorCode BuiltInRoleReadOnly { get; } = new("auth.built_in_role_read_only", StatusCodes.Status409Conflict, "'{0}' is a built-in role; copy it to make an editable one.", "auth-built-in-role-read-only", ErrorSeverity.Error);

    /// <summary>A permission name is not in the catalog.</summary>
    public static ErrorCode UnknownPermission { get; } = new("auth.unknown_permission", StatusCodes.Status400BadRequest, "'{0}' is not a permission in the catalog.", "auth-unknown-permission", ErrorSeverity.Error);

    /// <summary>The role's name or description is missing or too long.</summary>
    public static ErrorCode InvalidRole { get; } = new("auth.invalid_role", StatusCodes.Status400BadRequest, "{0}", "auth-invalid-role", ErrorSeverity.Error);

    /// <summary>No assignment with that id.</summary>
    public static ErrorCode AssignmentNotFound { get; } = new("auth.assignment_not_found", StatusCodes.Status404NotFound, "Assignment {0} does not exist.", "auth-assignment-not-found", ErrorSeverity.Error);

    /// <summary>No user with that id.</summary>
    public static ErrorCode UserNotFound { get; } = new("auth.user_not_found", StatusCodes.Status404NotFound, "User {0} does not exist.", "auth-user-not-found", ErrorSeverity.Error);
}
