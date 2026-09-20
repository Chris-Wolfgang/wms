// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolfgang.Wms.Core.Http;

namespace Wolfgang.Wms.UnitTests.Http;

public sealed class PreconditionsTests
{
    private static readonly EntityTag Current = EntityTag.FromRowVersion(0x2A);



    [Fact]
    public void A_matching_If_Match_lets_the_handler_run()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = Current.Value;

        Assert.Null(Preconditions.RequireIfMatch(context.Request, Current));
    }



    [Fact]
    public void A_stale_If_Match_is_412_with_the_precondition_failed_code()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = EntityTag.FromRowVersion(0x29).Value;

        var problem = Assert.IsType<ProblemHttpResult>(Preconditions.RequireIfMatch(context.Request, Current));

        Assert.Equal(StatusCodes.Status412PreconditionFailed, problem.StatusCode);
        Assert.Equal("concurrency.precondition_failed", problem.ProblemDetails.Extensions[ApiProblems.CodeExtension]);
    }



    [Fact]
    public void A_missing_If_Match_is_428_with_the_precondition_required_code()
    {
        var context = new DefaultHttpContext();

        var problem = Assert.IsType<ProblemHttpResult>(Preconditions.RequireIfMatch(context.Request, Current));

        Assert.Equal(StatusCodes.Status428PreconditionRequired, problem.StatusCode);
        Assert.Equal("concurrency.precondition_required", problem.ProblemDetails.Extensions[ApiProblems.CodeExtension]);
        Assert.Throws<ArgumentNullException>(() => Preconditions.RequireIfMatch(null!, Current));
    }
}
