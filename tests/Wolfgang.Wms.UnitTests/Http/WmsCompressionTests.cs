// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Http;

namespace Wolfgang.Wms.UnitTests.Http;

public sealed class WmsCompressionTests
{
    [Fact]
    public void AddWmsCompression_registers_brotli_then_gzip_for_https_on_the_text_media_types()
    {
        using var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<ResponseCompressionOptions>>().Value;

        Assert.True(options.EnableForHttps);
        Assert.Equal(WmsCompression.CompressedMediaTypes, options.MimeTypes);
        Assert.Contains("application/problem+json", options.MimeTypes);
        Assert.NotNull(provider.GetService<IResponseCompressionProvider>());
    }



    [Fact]
    public async Task UseWmsCompression_turns_https_compression_off_for_an_opted_out_endpoint_only()
    {
        using var provider = BuildProvider();
        var optedOut = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(DisableResponseCompressionMetadata.Instance), "auth");
        var ordinary = new Endpoint(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, "list");

        var optedOutMode = await ModeSeenByTheEndpoint(optedOut, provider);
        var ordinaryMode = await ModeSeenByTheEndpoint(ordinary, provider);
        var noFeatureMode = await ModeSeenByTheEndpoint(optedOut, provider, acceptsCompression: false);

        Assert.Equal(HttpsCompressionMode.DoNotCompress, optedOutMode);
        Assert.Equal(HttpsCompressionMode.Default, ordinaryMode);
        Assert.Null(noFeatureMode);
    }



    [Fact]
    public void DisableResponseCompression_adds_the_marker_metadata()
    {
        var builder = new TestConventionBuilder();

        builder.DisableResponseCompression();

        Assert.Contains(builder.Metadata, m => ReferenceEquals(m, DisableResponseCompressionMetadata.Instance));
    }



    [Fact]
    public void Extensions_when_the_argument_is_null_throw()
    {
        Assert.Throws<ArgumentNullException>(() => WmsCompression.AddWmsCompression(null!));
        Assert.Throws<ArgumentNullException>(() => WmsCompression.UseWmsCompression(null!));
        Assert.Throws<ArgumentNullException>(() => WmsCompression.DisableResponseCompression<TestConventionBuilder>(null!));
    }



    /// <summary>
    /// Runs an https request through UseWmsCompression and reports the mode of the compression feature as the
    /// endpoint sees it (null when the middleware did not run because the client accepts no encoding). The
    /// compression middleware restores the original features after the request, so the mode is captured inside.
    /// </summary>
    private static async Task<HttpsCompressionMode?> ModeSeenByTheEndpoint(Endpoint endpoint, IServiceProvider provider, bool acceptsCompression = true)
    {
        HttpsCompressionMode? seen = null;
        var app = new ApplicationBuilder(provider);
        app.UseWmsCompression();
        app.Run(context =>
        {
            seen = context.Features.Get<IHttpsCompressionFeature>()?.Mode;
            return Task.CompletedTask;
        });
        var pipeline = app.Build();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Scheme = "https";
        if (acceptsCompression)
        {
            context.Request.Headers.AcceptEncoding = "br";
        }

        context.SetEndpoint(endpoint);

        await pipeline(context);

        return seen;
    }



    private sealed class TestConventionBuilder : IEndpointConventionBuilder
    {
        private readonly EndpointBuilder _builder = new RouteEndpointBuilder(null, Microsoft.AspNetCore.Routing.Patterns.RoutePatternFactory.Parse("/"), 0);

        public IList<object> Metadata => _builder.Metadata;

        public void Add(Action<EndpointBuilder> convention)
        {
            convention(_builder);
        }
    }



    private static ServiceProvider BuildProvider()
    {
        return new ServiceCollection()
            .AddLogging()
            .AddWmsCompression()
            .BuildServiceProvider();
    }
}
