// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Wolfgang.Wms.Core.Identity;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Identity;
using Wolfgang.Wms.Infrastructure.Integrity;
using Wolfgang.Wms.UnitTests.Database;

namespace Wolfgang.Wms.UnitTests.Identity;

/// <summary>
/// E10.4 without a database: the canonical content of each signed row covers the security-relevant fields
/// only and is stable; the interceptor finds the rows a save must sign (including a role whose permission
/// rows changed) and refuses the synchronous save; the registrations resolve; the guards hold.
/// </summary>
public sealed class IntegrityUnitTests
{
    private const string SqlServer = "Microsoft.EntityFrameworkCore.SqlServer";



    [Fact]
    public void Canonical_content_covers_the_decision_fields_and_orders_permissions()
    {
        var user = new User { Id = 7, UserNameNormalized = "ALICE", PasswordHash = "hash", MustChangePassword = true, IsDisabled = false, IsLocalAdmin = true, SessionsValidAfter = new DateTimeOffset(2026, 9, 20, 8, 0, 0, 123, TimeSpan.FromHours(2)).AddTicks(4567), DisplayName = "changes freely" };
        var role = new Role { Id = 3, NameNormalized = "PICKER", BuiltInKey = null, Description = "changes freely" };
        role.Permissions.AddRange([new RolePermission { PermissionName = "b" }, new RolePermission { PermissionName = "a" }]);
        var assignment = new UserRole { Id = 9, UserId = 7, RoleId = 3, SiteId = 5, ExpiresAt = null, UpdatedBy = "changes freely" };

        Assert.Equal("user\nALICE\nhash\n1\n0\n1\n2026-09-20T06:00:00.123Z\n\n", user.CanonicalContent());   // UTC, millisecond precision: the same before the write and after the read
        Assert.Equal("role\nPICKER\n\na,b", role.CanonicalContent());
        Assert.Equal("user_role\n7\n3\n5\n", assignment.CanonicalContent());
        Assert.Equal("user\n\n\n0\n0\n0\n\n\n", new User().CanonicalContent());
        Assert.Equal("user\nBOB\n\n0\n0\n0\n\noidc\nsub-1", new User { UserNameNormalized = "BOB", Provider = "oidc", ProviderSubject = "sub-1" }.CanonicalContent());   // E11.1: the identity binding is signed
        Assert.Equal("user_role\n0\n0\n\n2026-09-20T06:00:00.123Z", new UserRole { ExpiresAt = user.SessionsValidAfter }.CanonicalContent());
    }



    [Fact]
    public void Pending_rows_are_added_or_changed_signed_entities_and_roles_whose_permissions_changed()
    {
        using var context = new WmsDbContext(ModelConventionsTests.Options<WmsDbContext>(SqlServer));
        var added = new User { Id = 1, UserNameNormalized = "A" };
        var untouched = new User { Id = 2, UserNameNormalized = "B" };
        var deleted = new User { Id = 3, UserNameNormalized = "C" };
        var grown = new Role { Id = 10, NameNormalized = "GROWN" };
        var shrunk = new Role { Id = 11, NameNormalized = "SHRUNK" };
        shrunk.Permissions.Add(new RolePermission { Id = 100, RoleId = 11, PermissionName = "x" });
        var newRole = new Role { Id = 12, NameNormalized = "NEW" };
        newRole.Permissions.Add(new RolePermission { PermissionName = "y" });
        context.Add(added);
        context.Attach(untouched);
        context.Remove(deleted);
        context.Attach(grown);
        context.Attach(shrunk);
        context.Add(newRole);
        grown.Permissions.Add(new RolePermission { PermissionName = "z" });
        context.Remove(shrunk.Permissions[0]);

        var pending = IntegritySigningInterceptor.Pending(context);

        Assert.Equal([added, grown, shrunk, newRole], pending.OrderBy(p => p.Id).Cast<object>());
        Assert.Throws<ArgumentNullException>(() => IntegritySigningInterceptor.Pending(null!));
    }



    [Fact]
    public void An_added_row_whose_principal_is_added_in_the_same_save_is_refused()
    {
        using var context = new WmsDbContext(ModelConventionsTests.Options<WmsDbContext>(SqlServer));
        var role = new Role { NameNormalized = "NEW" };
        context.Add(role);
        context.Add(new UserRole { UserId = 1, Role = role });

        var refused = Assert.Throws<InvalidOperationException>(() => IntegritySigningInterceptor.Pending(context));

        Assert.Contains("UserRole.RoleId", refused.Message, StringComparison.Ordinal);
    }



    [Fact]
    public async Task The_interceptor_signs_through_the_signer_and_checks_its_arguments()
    {
        var signer = new CountingSigner();
        var interceptor = new IntegritySigningInterceptor(signer);
        var user = new User { UserNameNormalized = "A" };

        await signer.SignAllAsync([user], CancellationToken.None);

        Assert.Equal(user.CanonicalContent(), user.Signature);
        Assert.Equal(1, signer.Signed);
        Assert.True(await signer.IsValidAsync(user, "core.user", CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => interceptor.SavingChanges(null!, default));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await interceptor.SavingChangesAsync(null!, default, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => new IntegritySigningInterceptor(null!));
    }



    [Fact]
    public void Registrations_resolve_and_the_guards_hold()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddWmsIntegritySigning().AddWmsIntegrityVerification();
        using var provider = services.BuildServiceProvider();

        using var signer = provider.GetRequiredService<IntegritySigner>();
        var job = provider.GetRequiredService<IntegrityVerificationJob>();

        Assert.Same(signer, provider.GetRequiredService<IIntegritySigner>());
        Assert.Same(job, provider.GetServices<IHostedService>().OfType<IntegrityVerificationJob>().Single());
        Assert.Null(job.LastResult);
        Assert.NotNull(provider.GetRequiredService<IntegritySigningInterceptor>());
        Assert.Throws<ArgumentNullException>(() => IntegrityServiceCollectionExtensions.AddWmsIntegritySigning(null!));
        Assert.Throws<ArgumentNullException>(() => IntegrityServiceCollectionExtensions.AddWmsIntegrityVerification(null!));
        Assert.Throws<ArgumentNullException>(() => new IntegritySigner(null!, NullLogger<IntegritySigner>.Instance));
        Assert.Throws<ArgumentNullException>(() => new IntegritySigner(provider.GetRequiredService<IServiceScopeFactory>(), null!));
        Assert.Throws<ArgumentNullException>(() => new IntegrityVerificationJob(null!, NullLogger<IntegrityVerificationJob>.Instance));
        Assert.Throws<ArgumentNullException>(() => new IntegrityVerificationJob(provider.GetRequiredService<IServiceScopeFactory>(), null!));
        Assert.Throws<ArgumentNullException>(() => new IntegrityBackfillCheck(null!, signer, NullLogger<IntegrityBackfillCheck>.Instance));
        Assert.Throws<ArgumentNullException>(() => new IntegrityBackfillCheck(provider.GetRequiredService<IServiceScopeFactory>(), null!, NullLogger<IntegrityBackfillCheck>.Instance));
        Assert.Throws<ArgumentNullException>(() => new IntegrityBackfillCheck(provider.GetRequiredService<IServiceScopeFactory>(), signer, null!));
    }



    [Fact]
    public async Task Signer_arguments_are_checked_before_the_key_is_touched()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        using var provider = services.BuildServiceProvider();
        using var signer = new IntegritySigner(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<IntegritySigner>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() => signer.SignAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => signer.IsValidAsync(null!, "t", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => signer.IsValidAsync(new User(), " ", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => signer.SignAllAsync(null!, CancellationToken.None));
        Assert.Equal(0, signer.Failures);
        Assert.NotNull(AuthErrorCodes.IntegrityFailure);
        Assert.Equal(403, AuthErrorCodes.IntegrityFailure.HttpStatus);
        Assert.Null(AuthSettings.IntegrityVerifyInterval.Validate(TimeSpan.FromHours(1)));
        Assert.NotNull(AuthSettings.IntegrityVerifyInterval.Validate(TimeSpan.FromSeconds(30)));
        Assert.NotNull(AuthSettings.IntegrityVerifyInterval.Validate(TimeSpan.FromDays(8)));
    }



    [Fact]
    public async Task Stopping_the_job_and_the_backfill_completes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        using var provider = services.BuildServiceProvider();
        using var signer = new IntegritySigner(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<IntegritySigner>.Instance);
        var check = new IntegrityBackfillCheck(provider.GetRequiredService<IServiceScopeFactory>(), signer, NullLogger<IntegrityBackfillCheck>.Instance);
        var job = new IntegrityVerificationJob(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<IntegrityVerificationJob>.Instance);

        await check.StopAsync(CancellationToken.None);
        await job.StopAsync(CancellationToken.None);
        job.Dispose();
    }



    private sealed class CountingSigner : IIntegritySigner
    {
        public int Signed { get; private set; }

        public Task<string> SignAsync(string content, CancellationToken cancellationToken)
        {
            Signed++;
            return Task.FromResult(content);
        }

        public Task<bool> IsValidAsync(ISignedEntity entity, string table, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public async Task SignAllAsync(IEnumerable<ISignedEntity> entities, CancellationToken cancellationToken)
        {
            foreach (var entity in entities)
            {
                entity.Signature = await SignAsync(entity.CanonicalContent(), cancellationToken);
            }
        }
    }
}
