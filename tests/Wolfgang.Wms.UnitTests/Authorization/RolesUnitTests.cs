// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Authorization;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Core.Modules;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Identity;
using Wolfgang.Wms.UnitTests.Database;

namespace Wolfgang.Wms.UnitTests.Authorization;

/// <summary>
/// E10.2/E10.3 without a database: built-in role names, default roles on permissions, the placeholder
/// store, the module registration, and the three tables' shapes.
/// </summary>
public sealed class RolesUnitTests
{
    [Fact]
    public void Built_in_roles_have_keys_names_and_descriptions()
    {
        Assert.Equal(["administrator", "supervisor", "resolver", "support", "viewer"], BuiltInRoles.All.Select(r => r.Key()));
        Assert.Equal("Administrator", BuiltInRole.Administrator.DisplayName());
        Assert.Contains("resolution lane", BuiltInRole.Resolver.Description(), StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => ((BuiltInRole)9).Key());
        Assert.Throws<ArgumentOutOfRangeException>(() => ((BuiltInRole)9).DisplayName());
        Assert.Throws<ArgumentOutOfRangeException>(() => ((BuiltInRole)9).Description());
    }



    [Fact]
    public void Permissions_name_the_roles_that_hold_them_by_default()
    {
        Assert.Empty(new Permission("a.b", "d").DefaultRoles);
        Assert.Equal([BuiltInRole.Supervisor, BuiltInRole.Resolver], ConsolePermissions.EnterResolve.DefaultRoles);
        Assert.Empty(ConsolePermissions.EnterConfigure.DefaultRoles);
        Assert.Contains(BuiltInRole.Support, RolesModule.Read.DefaultRoles);
        Assert.Empty(RolesModule.Write.DefaultRoles);
    }



    [Fact]
    public async Task Placeholder_roles_refuse_everything_but_grants_and_seeding()
    {
        var roles = new NoRoles();

        await AssertUnavailableAsync(() => roles.ListAsync(CancellationToken.None));
        await AssertUnavailableAsync(() => roles.FindAsync(1, CancellationToken.None));
        await AssertUnavailableAsync(() => roles.CreateAsync(new RoleDraft("x", "y", []), "me", CancellationToken.None));
        await AssertUnavailableAsync(() => roles.UpdateAsync(1, new RoleDraft("x", "y", []), "me", CancellationToken.None));
        await AssertUnavailableAsync(() => roles.CopyAsync(1, "x", "me", CancellationToken.None));
        await AssertUnavailableAsync(() => roles.DeleteAsync(1, "me", CancellationToken.None));
        await AssertUnavailableAsync(() => roles.AssignmentsOfAsync(1, CancellationToken.None));
        await AssertUnavailableAsync(() => roles.AssignAsync(1, 1, null, null, "me", CancellationToken.None));
        await AssertUnavailableAsync(() => roles.UnassignAsync(1, "me", CancellationToken.None));
        Assert.Empty(await roles.GrantsOfAsync(1, DateTimeOffset.UnixEpoch, CancellationToken.None));
        Assert.Equal(0, await roles.EnsureBuiltInAsync(new PermissionCatalog(new ModuleCollection()), CancellationToken.None));
    }



    [Fact]
    public void Module_registers_the_placeholder_and_declares_its_permissions()
    {
        var services = new ServiceCollection();

        services.AddWmsRolesModule();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<NoRoles>(provider.GetRequiredService<IRoles>());
        Assert.Equal(["auth.roles.read", "auth.roles.write"], provider.GetRequiredService<ModuleCollection>().Modules.Single(m => string.Equals(m.Name, "roles", StringComparison.Ordinal)).Permissions.Select(p => p.Name));
        Assert.Throws<ArgumentNullException>(() => RolesModule.AddWmsRolesModule(null!));
        Assert.Equal("\"2a\"", new RoleInfo(1, "x", "y", null, [], 0x2A).Etag);
    }



    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void Role_tables_are_core_role_role_permission_and_user_role(string provider)
    {
        using var context = new WmsDbContext(ModelConventionsTests.Options<WmsDbContext>(provider));
        var role = context.Model.FindEntityType(typeof(Role))!;
        var permission = context.Model.FindEntityType(typeof(RolePermission))!;
        var assignment = context.Model.FindEntityType(typeof(UserRole))!;

        Assert.Equal(("core", "role"), (role.GetSchema(), role.GetTableName()));
        Assert.Equal(("core", "role_permission"), (permission.GetSchema(), permission.GetTableName()));
        Assert.Equal(("core", "user_role"), (assignment.GetSchema(), assignment.GetTableName()));
        Assert.Contains("ux_role_name_normalized", role.GetIndexes().Select(i => i.GetDatabaseName()));
        Assert.Contains("ux_role_permission_role_id_permission_name", permission.GetIndexes().Select(i => i.GetDatabaseName()));
        Assert.Contains("ux_user_role_user_id_role_id_site_id", assignment.GetIndexes().Select(i => i.GetDatabaseName()));
        Assert.Equal(["trg_role_row_version"], role.GetDeclaredTriggers().Select(t => t.ModelName));
        Assert.Equal(["trg_user_role_row_version"], assignment.GetDeclaredTriggers().Select(t => t.ModelName));
        Assert.Equal("ALICE", EfRoles.Normalize(" alice "));
        Assert.Throws<ArgumentNullException>(() => EfRoles.Normalize(null!));
        Assert.Throws<ArgumentNullException>(() => new RoleConfiguration().Configure((Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Role>)null!));
        Assert.Throws<ArgumentNullException>(() => new RoleConfiguration().Configure((Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<RolePermission>)null!));
        Assert.Throws<ArgumentNullException>(() => new RoleConfiguration().Configure((Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<UserRole>)null!));
        Assert.NotNull(context.Roles);
        Assert.NotNull(context.UserRoles);
    }



    private static async Task AssertUnavailableAsync(Func<Task> action)
    {
        var failure = await Assert.ThrowsAsync<AuthException>(action);
        Assert.Equal(AuthErrorCodes.Unavailable, failure.Code);
    }
}
