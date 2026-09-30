// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.UnitTests.Http;

public sealed class ApiProblemsTests
{
    private static readonly ErrorCode ToteMissing = new("picking.tote_missing", StatusCodes.Status404NotFound, "Tote {0} is not on the line.", "tote-missing", ErrorSeverity.Warning);



    [Fact]
    public void Problem_builds_status_title_type_and_code_extensions_from_the_error_code()
    {
        var result = ApiProblems.Problem(ToteMissing, "Scanned at station 4.", "T-17");

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(404, problem.StatusCode);
        Assert.Equal("Tote T-17 is not on the line.", problem.ProblemDetails.Title);
        Assert.Equal("Scanned at station 4.", problem.ProblemDetails.Detail);
        Assert.Equal(ApiProblems.DocsBase + "#tote-missing", problem.ProblemDetails.Type);
        Assert.Equal("picking.tote_missing", problem.ProblemDetails.Extensions[ApiProblems.CodeExtension]);
        Assert.Equal("warning", problem.ProblemDetails.Extensions[ApiProblems.SeverityExtension]);
    }



    [Fact]
    public void Title_without_arguments_is_the_raw_template()
    {
        Assert.Equal("Tote {0} is not on the line.", ApiProblems.Title(ToteMissing));
        Assert.Equal("Tote T-1 is not on the line.", ApiProblems.Title(ToteMissing, "T-1"));
    }



    [Fact]
    public void AddWmsProblemDetails_registers_problem_details_with_the_trace_id_customisation()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddWmsProblemDetails()
            .BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value;
        var context = new ProblemDetailsContext { HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-1" } };

        options.CustomizeProblemDetails!(context);

        Assert.NotNull(provider.GetService<IProblemDetailsService>());
        Assert.Equal("trace-1", context.ProblemDetails.Extensions["traceId"]);
    }



    [Fact]
    public void Members_when_the_argument_is_null_throw()
    {
        Assert.Throws<ArgumentNullException>(() => ApiProblems.Problem(null!));
        Assert.Throws<ArgumentNullException>(() => ApiProblems.Title(null!));
        Assert.Throws<ArgumentNullException>(() => ApiProblems.TypeUri(null!));
        Assert.Throws<ArgumentNullException>(() => ApiProblems.AddWmsProblemDetails(null!));
        Assert.Throws<ArgumentNullException>(() => ApiProblems.UseWmsProblemDetails(null!));
    }
}
