// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Wolfgang.AuditTrail;
using Wolfgang.AuditTrail.Entities;
using Wolfgang.Wms.Infrastructure.Database.Auditing;
using Wolfgang.Wms.IntegrationTests.Database.TestModels;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E6.4's first test against the library, on a throwaway audited context: what an aggregate save records
/// (owned entities, complex types, several rows in one transaction), what a database cascade delete does not,
/// and which header columns exist for ordering and correlation. Each finding that is a gap is filed upstream
/// (see the PR); the assertions pin today's behaviour so an upstream change is noticed here.
/// </summary>
public sealed class AuditTrailCapabilityTests
{
    [DockerFact]
    public async Task Aggregate_saves_owned_types_and_cascade_deletes_behave_as_documented()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();
        var builder = new DbContextOptionsBuilder<AuditCapabilityDbContext>().UseNpgsql(container.GetConnectionString());
        using var context = new AuditCapabilityDbContext(builder.Options);
        await context.Database.EnsureCreatedAsync();

        var parent = new AuditedParent
        {
            Name = "zone group",
            Address = new AuditedAddress { Street = "Main St" },
            Size = new AuditedSize { Width = 2, Height = 3 },
            Children = [new AuditedChild { Label = "a" }, new AuditedChild { Label = "b" }],
        };
        context.Parents.Add(parent);
        await context.SaveChangesAsync();
        var insertHeaders = await context.Set<AuditHeader>().Include(h => h.Details).ToListAsync();

        context.ChangeTracker.Clear();
        var fresh = await context.Parents.SingleAsync(p => p.Id == parent.Id);   // owned address comes along; children stay unloaded
        context.Parents.Remove(fresh);   // children go by database cascade
        await context.SaveChangesAsync();
        var deleteHeaders = await context.Set<AuditHeader>().Where(h => h.Operation == AuditOperation.Delete).ToListAsync();

        // One transaction id per save; the parent, the owned address and both children each get a header.
        Assert.Single(insertHeaders.Select(h => h.TransactionId).Distinct());
        Assert.Equal(["AuditedAddress", "AuditedChild", "AuditedChild", "AuditedParent"], insertHeaders.Select(h => h.EntityType.Split('.')[^1]).Order(StringComparer.Ordinal));
        // Complex-type members are NOT captured today (the capture walks entry.Properties only): upstream gap.
        var parentDetails = insertHeaders.Single(h => h.EntityType.EndsWith("AuditedParent", StringComparison.Ordinal)).Details.Select(d => d.ColumnName).ToList();
        Assert.Contains("name", parentDetails);
        Assert.DoesNotContain(parentDetails, c => c.Contains("width", StringComparison.OrdinalIgnoreCase));
        // Database cascade deletes are not captured: only what the change tracker knew about.
        Assert.DoesNotContain(deleteHeaders, h => h.EntityType.EndsWith("AuditedChild", StringComparison.Ordinal));
        Assert.Contains(deleteHeaders, h => h.EntityType.EndsWith("AuditedParent", StringComparison.Ordinal));
        // Ordering within a transaction and a WMS correlation are not part of the header today (upstream).
        Assert.DoesNotContain(typeof(AuditHeader).GetProperties(), p => p.Name.Contains("Sequence", StringComparison.Ordinal) || p.Name.Contains("Order", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(AuditHeader).GetProperties(), p => p.Name.Contains("Correlation", StringComparison.Ordinal));
        Assert.Equal(WmsAuditing.SystemIdentity, insertHeaders[0].UserId);
    }
}
