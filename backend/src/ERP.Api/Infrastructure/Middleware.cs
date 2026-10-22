using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Common;
using ERP.Infrastructure.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Api.Infrastructure;

/// <summary>
/// Resolves the X-Entity-Id header: it must be a legal entity of the caller's workspace and inside the caller's
/// allowed set, otherwise 403. Org-related query/header/cookie values from the client are never trusted.
/// </summary>
public sealed class LegalEntityScopeMiddleware(RequestDelegate next)
{
    public const string Header = "X-Entity-Id";

    public async Task InvokeAsync(HttpContext http, ICurrentUser user, IAppDbContext db)
    {
        if (user.IsAuthenticated && http.Request.Headers.TryGetValue(Header, out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            if (!Guid.TryParse(raw, out var id)
                || (user.AllowedLegalEntityIds is { } allowed && !allowed.Contains(id))
                || !await db.LegalEntities.AnyAsync(e => e.Id == id, http.RequestAborted))
            {
                http.Response.StatusCode = StatusCodes.Status403Forbidden;
                await http.Response.WriteAsJsonAsync(new ProblemDetails { Status = 403, Title = "Legal entity not allowed", Detail = "You cannot work in that legal entity." });
                return;
            }
            http.Items["erp.legalEntityId"] = id;
        }
        await next(http);
    }
}

/// <summary>Maps exceptions to RFC 7807 ProblemDetails.</summary>
public sealed class ProblemExceptionHandler(ILogger<ProblemExceptionHandler> log, IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        ProblemDetails problem = ex switch
        {
            FieldValidationException v => new ValidationProblemDetails(v.Errors) { Status = 400, Title = "Validation failed" },
            ValidationException fv => new ValidationProblemDetails(fv.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())) { Status = 400, Title = "Validation failed" },
            NotFoundException nf => new ProblemDetails { Status = 404, Title = "Not found", Detail = nf.Message },
            ForbiddenException fb => new ProblemDetails { Status = 403, Title = "Forbidden", Detail = fb.Message },
            ConflictException c => new ProblemDetails { Status = 409, Title = "Conflict", Detail = c.Message },
            DomainException d => new ProblemDetails { Status = 409, Title = "Rule violated", Detail = d.Message },
            AuthException a => new ProblemDetails { Status = a.Status, Title = a.Status == 423 ? "Account locked" : "Authentication failed", Detail = a.Message },
            DbUpdateConcurrencyException => new ProblemDetails { Status = 409, Title = "Concurrency conflict", Detail = "The record was changed by someone else. Reload and try again." },
            BadHttpRequestException b => new ProblemDetails { Status = b.StatusCode, Title = "Bad request", Detail = b.Message },
            System.Text.Json.JsonException j => new ProblemDetails { Status = 400, Title = "Invalid JSON", Detail = j.Message },
            _ => new ProblemDetails { Status = 500, Title = "Unexpected error", Detail = env.IsDevelopment() ? ex.Message : "An unexpected error occurred." },
        };
        if (problem.Status >= 500) log.LogError(ex, "Unhandled exception");
        if (ex is AuthException { RetryAt: { } retryAt })
        {
            problem.Extensions["retryAt"] = retryAt;
            http.Response.Headers.RetryAfter = Math.Max(1, (int)(retryAt - DateTime.UtcNow).TotalSeconds).ToString();
        }
        problem.Extensions["traceId"] = http.TraceIdentifier;
        http.Response.StatusCode = problem.Status ?? 500;
        await http.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json", ct);
        return true;
    }
}
