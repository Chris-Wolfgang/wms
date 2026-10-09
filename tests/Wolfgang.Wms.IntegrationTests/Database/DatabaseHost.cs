// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Wolfgang.Wms.Infrastructure.Database;

namespace Wolfgang.Wms.IntegrationTests.Database;

/// <summary>
/// A host with the database module alone, started directly, for the tests that expect startup to be refused
/// (E2.1 options validation, E4.4 schema behind, E4.6 schema ahead, unreachable server). <c>Program</c> composes
/// the same module (<c>AddWmsDatabase</c>), but a <c>WebApplicationFactory&lt;Program&gt;</c> host that refuses
/// to start races the entry point's disposal of it and can surface <see cref="ObjectDisposedException"/> instead
/// of the refusal (Chris-Wolfgang/wms#840); here the exception comes straight out of
/// <see cref="WebApplication.StartAsync"/>. Hosts that start successfully keep going through the factory, which
/// is what proves <c>Program</c>'s composition.
/// </summary>
internal static class DatabaseHost
{
    /// <summary>
    /// Builds and starts the host; throws what startup throws, with the host disposed.
    /// </summary>
    public static async Task<WebApplication> StartAsync(string provider, string connectionString, bool trustServerCertificate)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Wms:Database:Provider"] = provider,
            ["Wms:Database:ConnectionString"] = connectionString,
            ["Wms:Database:TrustServerCertificate"] = trustServerCertificate ? "true" : "false",
        });
        builder.Services.AddWmsDatabase(builder.Configuration);
        var app = builder.Build();
        try
        {
            await app.StartAsync();
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        return app;
    }
}
