using System.Linq.Expressions;
using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace ERP.Modularity;

/// <summary>
/// Wraps every endpoint result in <see cref="ApiResponse{T}"/> (paged results in <see cref="ApiPagedResponse{T}"/>).
/// Endpoints return plain values / Results.Ok / Results.Created / Results.Problem; nothing is wrapped by hand.
/// Files, redirects, 204 and challenge/forbid results pass through unchanged.
/// </summary>
public sealed class ApiEnvelopeFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var result = await next(ctx);
        return ApiEnvelope.Wrap(result, ctx.HttpContext);
    }
}

public static class ApiEnvelope
{
    public static object? Wrap(object? result, HttpContext http)
    {
        var traceId = http.TraceIdentifier;
        switch (result)
        {
            case null:
                return Results.Json(ApiResponses.Ok(null, 200, traceId), statusCode: 200);
            case ProblemHttpResult p:
                return ProblemResult(p.ProblemDetails, http);
            case ValidationProblem vp:
                return ProblemResult(vp.ProblemDetails, http);
            case IValueHttpResult v when result is IStatusCodeHttpResult s && result is not IFileHttpResult:
            {
                var status = s.StatusCode ?? 200;
                if (v.Value is ProblemDetails pd) return ProblemResult(pd, http);
                if (result.GetType().GetProperty("Location")?.GetValue(result) is string location) http.Response.Headers.Location = location;
                return Results.Json(Body(v.Value, status, traceId), statusCode: status);
            }
            case IResult:
                return result; // NoContent, files, redirects, challenge/forbid, already-wrapped JSON
            default:
                return Results.Json(Body(result, 200, traceId), statusCode: 200);
        }
    }

    private static object Body(object? value, int status, string traceId) => value switch
    {
        ApiResponse<object?> already => already,
        IPagedResult page => ApiResponses.Paged(page, status, traceId),
        _ => ApiResponses.Ok(value, status, traceId),
    };

    private static IResult ProblemResult(ProblemDetails p, HttpContext http)
    {
        var status = p.Status ?? 500;
        var errors = p is HttpValidationProblemDetails v ? v.Errors : null;
        var ext = p.Extensions.Where(kv => kv.Key != "traceId").ToDictionary(kv => kv.Key, kv => kv.Value);
        return Results.Json(ApiResponses.Error(status, p.Title, p.Detail, http.TraceIdentifier, errors, p.Type, ext),
            statusCode: status, contentType: "application/problem+json");
    }

    /// <summary>Writes an error envelope from middleware (module gate, entity scope, status-code pages, exception handler).</summary>
    public static Task WriteErrorAsync(HttpContext http, int status, string? title, string? detail, string? type = null,
        IDictionary<string, string[]>? errors = null, IDictionary<string, object?>? extensions = null)
    {
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(ApiResponses.Error(status, title, detail, http.TraceIdentifier, errors, type, extensions),
            options: null, contentType: "application/problem+json", cancellationToken: http.RequestAborted);
    }
}

/// <summary>One-line generic list endpoints. They only send a MediatR query; search, filter, sort and paging come from the entity's attributes.</summary>
public static class ListEndpointExtensions
{
    /// <summary><c>group.MapPagedList&lt;Item, ItemDto&gt;("", CorePermissions.ItemsRead, i =&gt; new ItemDto(...))</c></summary>
    public static RouteHandlerBuilder MapPagedList<T, TDto>(this IEndpointRouteBuilder group, string pattern, string permission,
        Expression<Func<T, TDto>> projection) where T : Entity =>
        group.MapGet(pattern, ([AsParameters] PagedQuery q, ISender sender, CancellationToken ct) => sender.Send(new ListQuery<T, TDto>(q, projection, permission), ct))
            .RequirePermission(permission);

    /// <summary>Same, for entities with custom fields: values are projected through the entity type's definitions.</summary>
    public static RouteHandlerBuilder MapPagedList<T, TDto>(this IEndpointRouteBuilder group, string pattern, string permission,
        string customFieldEntityType, Func<T, JsonObject, TDto> map) where T : Entity, IHasCustomFields =>
        group.MapGet(pattern, ([AsParameters] PagedQuery q, ISender sender, CancellationToken ct) =>
                sender.Send(new ListWithCustomFieldsQuery<T, TDto>(q, customFieldEntityType, map, permission), ct))
            .RequirePermission(permission);
}
