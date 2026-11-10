using ERP.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modularity;

/// <summary>
/// Builds the standard <see cref="ApiResponse{T}"/> envelope. Controllers never call it: <see cref="Web.ApiEnvelopeResultFilter"/>
/// wraps their results. Middleware (module gate, entity scope, status-code pages, exception handler) writes errors with <see cref="WriteErrorAsync"/>.
/// </summary>
public static class ApiEnvelope
{
    public const string ProblemContentType = "application/problem+json";

    /// <summary>The success envelope for a value: a page gets <see cref="ApiPagedResponse{T}"/>, an envelope passes through unchanged.</summary>
    public static object Body(object? value, int status, string traceId) => value switch
    {
        ApiResponse<object?> already => already,
        IPagedResult page => ApiResponses.Paged(page, status, traceId),
        _ => ApiResponses.Ok(value, status, traceId),
    };

    /// <summary>The error envelope for a <see cref="ProblemDetails"/> (validation errors and extensions kept).</summary>
    public static object ProblemBody(ProblemDetails p, HttpContext http)
    {
        var errors = p is HttpValidationProblemDetails v ? v.Errors : null;
        var ext = p.Extensions.Where(kv => kv.Key != "traceId").ToDictionary(kv => kv.Key, kv => kv.Value);
        return ApiResponses.Error(p.Status ?? 500, p.Title, p.Detail, http.TraceIdentifier, errors, p.Type, ext);
    }

    /// <summary>Writes an error envelope from middleware (module gate, entity scope, status-code pages, exception handler).</summary>
    public static Task WriteErrorAsync(HttpContext http, int status, string? title, string? detail, string? type = null,
        IDictionary<string, string[]>? errors = null, IDictionary<string, object?>? extensions = null)
    {
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(ApiResponses.Error(status, title, detail, http.TraceIdentifier, errors, type, extensions),
            options: null, contentType: ProblemContentType, cancellationToken: http.RequestAborted);
    }
}
