using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Common;
using ERP.Application.Auth;
using ERP.Domain.Organizations;
using ERP.Modularity;
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

    public async Task InvokeAsync(HttpContext http, ICurrentUser user, IReadRepository<LegalEntity> legalEntities)
    {
        if (user.IsAuthenticated && http.Request.Headers.TryGetValue(Header, out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            if (!Guid.TryParse(raw, out var id)
                || (user.AllowedLegalEntityIds is { } allowed && !allowed.Contains(id))
                || !await legalEntities.AnyAsync(e => e.Id == id, http.RequestAborted))
            {
                await ApiEnvelope.WriteErrorAsync(http, StatusCodes.Status403Forbidden, "Legal entity not allowed", "You cannot work in that legal entity.");
                return;
            }
            http.Items["erp.legalEntityId"] = id;
        }
        await next(http);
    }
}

/// <summary>Maps exceptions to the standard error envelope (RFC 7807 members included).</summary>
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
            InvalidDataException d => new ProblemDetails { Status = 400, Title = "Bad request", Detail = d.Message }, // malformed multipart / form body
            BadHttpRequestException b => new ProblemDetails { Status = b.StatusCode, Title = "Bad request", Detail = b.Message },
            System.Text.Json.JsonException j => new ProblemDetails { Status = 400, Title = "Invalid JSON", Detail = j.Message },
            _ => new ProblemDetails { Status = 500, Title = "Unexpected error", Detail = env.IsDevelopment() || env.IsEnvironment("Testing") ? ex.ToString() : "An unexpected error occurred." },
        };
        if (problem.Status >= 500) log.LogError(ex, "Unhandled exception");
        if (ex is AuthException { RetryAt: { } retryAt })
        {
            problem.Extensions["retryAt"] = retryAt;
            http.Response.Headers.RetryAfter = Math.Max(1, (int)(retryAt - DateTime.UtcNow).TotalSeconds).ToString();
        }
        var errors = problem is ValidationProblemDetails vpd ? vpd.Errors : null;
        await ApiEnvelope.WriteErrorAsync(http, problem.Status ?? 500, problem.Title, problem.Detail, problem.Type, errors,
            problem.Extensions.ToDictionary(kv => kv.Key, kv => kv.Value));
        return true;
    }
}
