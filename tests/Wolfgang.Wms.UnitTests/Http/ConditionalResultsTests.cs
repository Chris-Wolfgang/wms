// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Wolfgang.Wms.Core.Http;

namespace Wolfgang.Wms.UnitTests.Http;

public sealed class ConditionalResultsTests
{
    private static readonly EntityTag Current = EntityTag.FromRowVersion(0x2A);
    private readonly IResult _body = Results.Ok("fresh");
    private bool _produced;



    [Fact]
    public void NotModifiedOr_when_If_None_Match_matches_returns_304_without_producing_the_body()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfNoneMatch = Current.Value;

        var result = ConditionalResults.NotModifiedOr(context.Request, Current, Produce);

        Assert.Equal(StatusCodes.Status304NotModified, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.False(_produced);
        Assert.Equal(Current.Value, context.Response.Headers.ETag);
        Assert.Equal(CacheControl.Api, context.Response.Headers.CacheControl);
    }



    [Fact]
    public void NotModifiedOr_when_If_None_Match_is_stale_produces_the_body_and_sets_validation_headers()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfNoneMatch = EntityTag.FromRowVersion(0x29).Value;

        var result = ConditionalResults.NotModifiedOr(context.Request, Current, Produce);

        Assert.Same(_body, result);
        Assert.True(_produced);
        Assert.Equal(Current.Value, context.Response.Headers.ETag);
        Assert.Equal(CacheControl.Api, context.Response.Headers.CacheControl);
    }



    [Fact]
    public void NotModifiedOr_when_no_If_None_Match_produces_the_body()
    {
        var context = new DefaultHttpContext();

        var result = ConditionalResults.NotModifiedOr(context.Request, Current, Produce);

        Assert.Same(_body, result);
        Assert.Equal(Current.Value, context.Response.Headers.ETag);
    }



    [Fact]
    public void NotModifiedOr_when_an_argument_is_null_throws()
    {
        var context = new DefaultHttpContext();

        Assert.Throws<ArgumentNullException>(() => ConditionalResults.NotModifiedOr(null!, Current, Produce));
        Assert.Throws<ArgumentNullException>(() => ConditionalResults.NotModifiedOr(context.Request, Current, null!));
    }



    [Fact]
    public void CacheControl_policies_always_revalidate_api_data_and_never_revalidate_hashed_assets()
    {
        Assert.Equal("private, no-cache", CacheControl.Api);
        Assert.Equal("public, max-age=31536000, immutable", CacheControl.StaticAsset);
    }



    private IResult Produce()
    {
        _produced = true;
        return _body;
    }
}
