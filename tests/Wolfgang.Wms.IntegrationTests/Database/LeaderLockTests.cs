// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Wolfgang.Wms.Core.Hosting;
using Wolfgang.Wms.Core.Jobs;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Database.Leader;
using Wolfgang.Wms.Infrastructure.Secrets;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// E12.1 and E12.6 against PostgreSQL: readiness reports the database check with the schema version; two
/// instances contend for a lock — the second is refused while the first holds and renews, takes over once
/// the lease has expired without renewal, and the first's release frees it at once.
/// </summary>
public sealed class LeaderLockTests
{
    [DockerFact]
    public async Task Readiness_reports_the_database_and_one_instance_leads_at_a_time()
    {
        await using var container = new PostgreSqlBuilder("postgres:16").Build();
        await container.StartAsync();
        await using var app = await StartHostAsync(container.GetConnectionString());
        using var client = app.GetTestClient();
        var first = app.Services.GetRequiredService<ILeaderLock>();
        var second = new EfLeaderLock(app.Services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, NullLogger<EfLeaderLock>.Instance);

        using var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        using var readyJson = JsonDocument.Parse(await ready.Content.ReadAsStringAsync());
        var database = readyJson.RootElement.GetProperty("checks").EnumerateArray().Single();

        await using var held = await first.TryAcquireAsync("job", TimeSpan.FromSeconds(1), CancellationToken.None);
        var contended = await second.TryAcquireAsync("job", TimeSpan.FromSeconds(1), CancellationToken.None);
        await Task.Delay(1500);   // past the lease: the holder's renewals kept it
        var stillContended = await second.TryAcquireAsync("job", TimeSpan.FromSeconds(1), CancellationToken.None);
        await held!.DisposeAsync();   // released
        await using var taken = await second.TryAcquireAsync("job", TimeSpan.FromSeconds(1), CancellationToken.None);

        var row = await RowAsync(app.Services, "job");
        Assert.True(await second.TakeOrRenewAsync("expiring", TimeSpan.FromMilliseconds(300), CancellationToken.None));   // a raw take without a renewing lease
        Assert.False(await ((EfLeaderLock)first).TakeOrRenewAsync("expiring", TimeSpan.FromSeconds(1), CancellationToken.None));
        await Task.Delay(500);
        Assert.True(await ((EfLeaderLock)first).TakeOrRenewAsync("expiring", TimeSpan.FromSeconds(1), CancellationToken.None));   // expired: taken over
        await ((EfLeaderLock)first).ReleaseAsync("expiring", CancellationToken.None);
        await second.ReleaseAsync("expiring", CancellationToken.None);   // not the holder: no effect

        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(("database", "Healthy"), (database.GetProperty("name").GetString(), database.GetProperty("status").GetString()));
        Assert.StartsWith("Schema ", database.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.NotNull(held);
        Assert.Null(contended);
        Assert.Null(stillContended);
        Assert.False(held.Lost.IsCancellationRequested);
        Assert.NotNull(taken);
        Assert.Equal(second.Holder, row.Holder);
        Assert.Equal(string.Empty, (await RowAsync(app.Services, "expiring")).Holder);
        await Assert.ThrowsAsync<ArgumentException>(() => first.TryAcquireAsync(" ", TimeSpan.FromSeconds(1), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => first.TryAcquireAsync("job", TimeSpan.Zero, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => new EfLeaderLock(null!, TimeProvider.System, NullLogger<EfLeaderLock>.Instance));
    }



    private static async Task<LeaderLock> RowAsync(IServiceProvider services, string name)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<WmsDbContext>().Set<LeaderLock>().AsNoTracking().SingleAsync(l => l.Name == name);
    }



    private static async Task<WebApplication> StartHostAsync(string connectionString)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Wms:Database:Provider"] = "PostgreSql",
            ["Wms:Database:ConnectionString"] = connectionString,
            ["Wms:Database:AutoMigrate"] = "true",
        });
        builder.Services.AddWmsHealth();
        builder.Services.AddWmsDataProtection(builder.Configuration);
        builder.Services.AddWmsDatabase(builder.Configuration);
        var app = builder.Build();
        app.MapWmsHealth();
        await app.StartAsync();
        return app;
    }
}
