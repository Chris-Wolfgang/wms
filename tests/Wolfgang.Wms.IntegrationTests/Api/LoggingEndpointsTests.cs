// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Logging;
using Wolfgang.Wms.Logging;

namespace Wolfgang.Wms.IntegrationTests.Api;

/// <summary>
/// E12.4 through the API without a database: the status needs the permission and reports the defaults;
/// an elevation that does not lower the level or runs too long is refused; a valid one is unavailable
/// until the settings store exists (503). The host's level switch is the boot level.
/// </summary>
public sealed class LoggingEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private readonly WebApplicationFactory<Program> _factory;



    public LoggingEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }



    [Fact]
    public async Task Status_elevation_and_refusals()
    {
        using var client = _factory.WithTestAuth().CreateClient();

        using var anonymous = await client.GetAsync(new Uri("/api/v0/system/logging", UriKind.Relative));
        using var status = await client.SendAsync(TestAuth.As(HttpMethod.Get, "/api/v0/system/logging", "logging.manage@organization"));
        using var wrongLevel = await client.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/system/logging/elevate", "logging.manage@organization", Body(new ElevateLogLevelRequest("Warning", 10))));
        using var tooLong = await client.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/system/logging/elevate", "logging.manage@organization", Body(new ElevateLogLevelRequest("Debug", 100000))));
        using var noStore = await client.SendAsync(TestAuth.As(HttpMethod.Post, "/api/v0/system/logging/elevate", "logging.manage@organization", Body(new ElevateLogLevelRequest("Debug", 10))));
        using var endNoStore = await client.SendAsync(TestAuth.As(HttpMethod.Delete, "/api/v0/system/logging/elevate", "logging.manage@organization"));
        var info = await status.Content.ReadFromJsonAsync<LoggingStatus>(Json);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal((Microsoft.Extensions.Logging.LogLevel.Information, Microsoft.Extensions.Logging.LogLevel.Information, (Microsoft.Extensions.Logging.LogLevel?)null, (DateTimeOffset?)null, 120), (info!.Level, info.EffectiveLevel, info.ElevatedLevel, info.ElevatedUntil, info.MaxElevationMinutes));
        Assert.Equal(HttpStatusCode.BadRequest, wrongLevel.StatusCode);
        Assert.Equal("logging.elevation_rejected", await CodeAsync(wrongLevel));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, noStore.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, endNoStore.StatusCode);
        Assert.Equal(Serilog.Events.LogEventLevel.Information, _factory.Services.GetRequiredService<WmsLogLevel>().Current);
    }



    private static StringContent Body<T>(T value)
    {
        return new StringContent(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json");
    }



    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString();
    }
}
