using ERP.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ERP.Modularity.Web;

/// <summary>
/// Wraps every controller result in the standard <see cref="ApiResponse{T}"/> envelope (paged results in <see cref="ApiPagedResponse{T}"/>,
/// problems in the error envelope). Controllers return plain values, <c>Created(...)</c> or <c>Problem(...)</c> and never wrap by hand.
/// Files, text, redirects, 204, challenge/forbid and streamed results pass through unchanged.
/// </summary>
public sealed class ApiEnvelopeResultFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        var http = context.HttpContext;
        switch (context.Result)
        {
            case ObjectResult { Value: ProblemDetails problem } result:
                result.StatusCode = problem.Status ??= result.StatusCode ?? 500;
                result.Value = ApiEnvelope.ProblemBody(problem, http);
                result.DeclaredType = null;
                result.ContentTypes.Clear();
                result.ContentTypes.Add(ApiEnvelope.ProblemContentType);
                break;
            case ObjectResult result:
                result.StatusCode ??= StatusCodes.Status200OK;
                result.Value = ApiEnvelope.Body(result.Value, result.StatusCode.Value, http.TraceIdentifier);
                result.DeclaredType = null;
                break;
            case EmptyResult or OkResult:
                context.Result = new ObjectResult(ApiResponses.Ok(null, StatusCodes.Status200OK, http.TraceIdentifier)) { StatusCode = StatusCodes.Status200OK };
                break;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
