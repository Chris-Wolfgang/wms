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
    public async Task UseWmsCompression_keeps_the_compression_middleware_out_of_an_opted_out_endpoint_only()
    {
        using var provider = BuildProvider();
        var optedOut = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(DisableResponseCompressionMetadata.Instance), "auth");
        var ordinary = new Endpoint(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, "list");

        var optedOutMode = await ModeSeenByTheEndpoint(optedOut, provider);
        var ordinaryMode = await ModeSeenByTheEndpoint(ordinary, provider);
        var noFeatureMode = await ModeSeenByTheEndpoint(ordinary, provider, acceptsCompression: false);   // the middleware ran and declined: no Accept-Encoding, no feature

        Assert.Null(optedOutMode);   // the compression middleware was bypassed, so it installed no feature: the opt-out is the one guard
        Assert.Equal(HttpsCompressionMode.Default, ordinaryMode);
        Assert.Null(noFeatureMode);
    }



    [Fact]
    public async Task UseWmsCompression_leaves_an_opted_out_request_as_the_client_sent_it()
    {
        using var provider = BuildProvider();
        var optedOut = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(DisableResponseCompressionMetadata.Instance), "auth");
        string? acceptEncodingSeen = null;
        var app = new ApplicationBuilder(provider);
        app.UseWmsCompression();
        app.Run(context =>
        {
            acceptEncodingSeen = context.Request.Headers.AcceptEncoding;
            return Task.CompletedTask;
        });
        var pipeline = app.Build();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Headers.AcceptEncoding = "br";
        context.SetEndpoint(optedOut);

        await pipeline(context);

        Assert.Equal("br", acceptEncodingSeen);
    }



    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    public async Task UseWmsCompression_sends_an_opted_out_body_uncompressed_on_either_scheme(string scheme)
    {
        using var provider = BuildProvider();
        var optedOut = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(DisableResponseCompressionMetadata.Instance), "auth");
        var ordinary = new Endpoint(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, "list");

        var optedOutEncoding = await ContentEncodingSent(optedOut, provider, scheme);
        var ordinaryEncoding = await ContentEncodingSent(ordinary, provider, scheme);

        Assert.Equal(string.Empty, optedOutEncoding);
        Assert.Equal("br", ordinaryEncoding);
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



    /// <summary>
    /// Runs a request through UseWmsCompression to an endpoint that writes a JSON body and reports the
    /// Content-Encoding the response carries (empty when the body went out as written).
    /// </summary>
    private static async Task<string> ContentEncodingSent(Endpoint endpoint, IServiceProvider provider, string scheme)
    {
        var app = new ApplicationBuilder(provider);
        app.UseWmsCompression();
        app.Run(async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"token\":\"" + new string('a', 2048) + "\"}");
        });
        var pipeline = app.Build();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Scheme = scheme;
        context.Request.Headers.AcceptEncoding = "br";
        using var body = new MemoryStream();
        context.Response.Body = body;
        context.SetEndpoint(endpoint);

        await pipeline(context);

        return context.Response.Headers.ContentEncoding.ToString();
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
