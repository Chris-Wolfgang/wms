// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Wolfgang.AuditTrail;
using Wolfgang.AuditTrail.Entities;
using Wolfgang.Wms.Infrastructure.Database;
using Wolfgang.Wms.Infrastructure.Database.Auditing;
using Wolfgang.Wms.Infrastructure.Database.Conventions;

namespace Wolfgang.Wms.UnitTests.Database;

/// <summary>
/// E6.4 without a database: the audit tables are part of the product model in <c>core</c> with snake_case
/// names, the user providers attribute changes to the host and the signed-in user, and the conventions
/// leave a library's keys and column types alone.
/// </summary>
public sealed class AuditingTests
{
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void Audit_tables_are_mapped_into_core_with_snake_case_names(string provider)
    {
        using var context = new WmsDbContext(ModelConventionsTests.Options<WmsDbContext>(provider));
        var header = context.Model.FindEntityType(typeof(AuditHeader))!;
        var detail = context.Model.FindEntityType(typeof(AuditDetail))!;

        Assert.Equal(("core", "audit_header"), (header.GetSchema(), header.GetTableName()));
        Assert.Equal(("core", "audit_detail"), (detail.GetSchema(), detail.GetTableName()));
        Assert.Contains("on_behalf_of_user_id", header.GetProperties().Select(p => p.GetColumnName()));
        Assert.Equal("header_id", detail.FindProperty(nameof(AuditDetail.HeaderId))!.GetColumnName());
        Assert.Equal("pk_audit_header", header.FindPrimaryKey()!.GetName());
        Assert.True(context.AuditOptions.CaptureDeletedValues);
        Assert.Empty(ModelConventions.Verify(context.Model));
        Assert.True(ModelConventions.IsLibraryOwned(typeof(AuditHeader)));
        Assert.False(ModelConventions.IsLibraryOwned(typeof(WmsDbContext)));
        Assert.Throws<ArgumentNullException>(() => ModelConventions.IsLibraryOwned(null!));
    }



    [Fact]
    public void Host_provider_records_the_application_and_the_requests_user()
    {
        var accessor = new HttpContextAccessor();
        var provider = new HttpAuditUserProvider(accessor, new Environment("Wolfgang.Wms.Api"));
        var anonymous = new HttpAuditUserProvider(new HttpContextAccessor(), new Environment(string.Empty));

        var outsideRequest = provider.GetCurrentUser();
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "test")) };
        var inRequest = provider.GetCurrentUser();
        accessor.HttpContext = new DefaultHttpContext();
        var anonymousRequest = provider.GetCurrentUser();

        Assert.Equal(new AuditUser("Wolfgang.Wms.Api"), outsideRequest);
        Assert.Equal(new AuditUser("Wolfgang.Wms.Api", "alice"), inRequest);
        Assert.Null(anonymousRequest.OnBehalfOfUserId);
        Assert.Equal(WmsAuditing.SystemIdentity, anonymous.GetCurrentUser().UserId);
        Assert.Equal(new AuditUser(WmsAuditing.SystemIdentity), SystemAuditUserProvider.Instance.GetCurrentUser());
        Assert.Throws<ArgumentNullException>(() => new HttpAuditUserProvider(null!, new Environment("x")));
        Assert.Equal(WmsAuditing.SystemIdentity, new HttpAuditUserProvider(accessor).GetCurrentUser().UserId);
    }



    [Fact]
    public void Registration_supplies_the_options_and_the_host_provider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new Environment("Wolfgang.Wms.Worker"));
        services.AddWmsAuditing();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<AuditOptions>();
        using var scope = provider.CreateScope();

        Assert.Equal(("core", "audit_header", "audit_detail", true), (options.Schema, options.HeaderTableName, options.DetailTableName, options.CaptureDeletedValues));
        Assert.IsType<HttpAuditUserProvider>(scope.ServiceProvider.GetRequiredService<IAuditUserProvider>());
        Assert.Equal("Wolfgang.Wms.Worker", scope.ServiceProvider.GetRequiredService<IAuditUserProvider>().GetCurrentUser().UserId);
        Assert.Throws<ArgumentNullException>(() => WmsAuditing.AddWmsAuditing(null!));
    }



    private sealed class Environment : IHostEnvironment
    {
        public Environment(string applicationName)
        {
            ApplicationName = applicationName;
        }

        public string EnvironmentName { get; set; } = "Test";

        public string ApplicationName { get; set; }

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
