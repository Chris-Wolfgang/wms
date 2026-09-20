// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Http;

/// <summary>
/// Problem-details errors with codes (E82.3): every error response is <c>application/problem+json</c> whose
/// <c>status</c>, <c>title</c> and <c>type</c> come from the typed <see cref="ErrorCode"/> (E1.13) and whose
/// extensions carry the code itself, its severity and the trace id. Handlers never invent a status code or a
/// message string; they pick a code.
/// </summary>
public static class ApiProblems
{
    /// <summary>
    /// Base of the troubleshooting reference every <c>type</c> URI points into; the anchor is the code's.
    /// </summary>
    public const string DocsBase = "https://chris-wolfgang.github.io/wms/troubleshooting/";



    /// <summary>
    /// Extension member carrying the error code (<c>picking.tote_missing</c>).
    /// </summary>
    public const string CodeExtension = "code";



    /// <summary>
    /// Extension member carrying the severity (<c>info</c>, <c>warning</c>, <c>error</c>).
    /// </summary>
    public const string SeverityExtension = "severity";



    /// <summary>
    /// Registers problem details for unhandled exceptions and status-code responses, adding the trace id to
    /// every problem so a customer can quote it in a support request.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWmsProblemDetails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier));
    }



    /// <summary>
    /// Maps unhandled exceptions and empty error responses to problem details. Call before endpoints.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    public static IApplicationBuilder UseWmsProblemDetails(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }



    /// <summary>
    /// The problem response for <paramref name="code"/>: status from the code, title from its message template
    /// formatted with <paramref name="arguments"/>, <c>type</c> pointing at the code's documentation anchor,
    /// and <c>code</c>/<c>severity</c> extensions.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="detail">Optional human-readable detail specific to this occurrence.</param>
    /// <param name="arguments">Values for the message template's placeholders.</param>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public static IResult Problem(ErrorCode code, string? detail = null, params object[] arguments)
    {
        ArgumentNullException.ThrowIfNull(code);

        return Results.Problem
        (
            detail: detail,
            statusCode: code.HttpStatus,
            title: Title(code, arguments),
            type: TypeUri(code),
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [CodeExtension] = code.Code,
                [SeverityExtension] = code.Severity.ToString().ToLowerInvariant(),
            }
        );
    }



    /// <summary>
    /// The code's message template with <paramref name="arguments"/> applied (invariant culture).
    /// </summary>
    public static string Title(ErrorCode code, params object[] arguments)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(arguments);

        return arguments.Length == 0
            ? code.MessageTemplate
            : string.Format(CultureInfo.InvariantCulture, code.MessageTemplate, arguments);
    }



    /// <summary>
    /// The <c>type</c> URI of a code: the troubleshooting page at the code's anchor.
    /// </summary>
    public static string TypeUri(ErrorCode code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return DocsBase + "#" + code.DocsAnchor;
    }
}
