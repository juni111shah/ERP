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

/// <summary>
/// Honours the <c>Idempotency-Key</c> header on POST requests: the first request with a key runs normally and its 2xx response is
/// stored; a retry with the same key (same user, route and body) gets that response back with <c>Idempotent-Replay: true</c> and
/// creates nothing. Failed responses release the key. Requests without the header are untouched.
/// </summary>
public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    public const string Header = "Idempotency-Key";

    public async Task InvokeAsync(HttpContext http, ICurrentUser user, IServiceScopeFactory scopes)
    {
        if (!HttpMethods.IsPost(http.Request.Method) || !user.IsAuthenticated || user.UserId is not { } userId || user.OrgId is not { } orgId
            || !http.Request.Path.StartsWithSegments("/api/v1") || !http.Request.Headers.TryGetValue(Header, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            await next(http);
            return;
        }
        var key = raw.ToString().Trim();
        if (key.Length > IdempotencyService.MaxKeyLength)
        {
            await ApiEnvelope.WriteErrorAsync(http, StatusCodes.Status400BadRequest, "Invalid Idempotency-Key", $"The Idempotency-Key header can be at most {IdempotencyService.MaxKeyLength} characters.");
            return;
        }

        http.Request.EnableBuffering();
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = Convert.ToHexString(await sha.ComputeHashAsync(http.Request.Body, http.RequestAborted));
        http.Request.Body.Position = 0;
        var route = http.Request.Path.Value!.ToLowerInvariant();

        IdempotencyBegin begin;
        await using (var scope = Scoped(scopes, orgId, out var svc))
            begin = await svc.BeginAsync(userId, key, route, hash, http.RequestAborted);

        switch (begin.Outcome)
        {
            case IdempotencyOutcome.Mismatch:
                await ApiEnvelope.WriteErrorAsync(http, StatusCodes.Status422UnprocessableEntity, "Idempotency-Key reused", "This Idempotency-Key was already used with a different request.");
                return;
            case IdempotencyOutcome.InProgress:
                await ApiEnvelope.WriteErrorAsync(http, StatusCodes.Status409Conflict, "Request in progress", "A request with this Idempotency-Key is still being processed.");
                return;
            case IdempotencyOutcome.Replay:
                http.Response.StatusCode = begin.StatusCode;
                http.Response.ContentType = begin.ContentType;
                http.Response.Headers["Idempotent-Replay"] = "true";
                await http.Response.WriteAsync(begin.Body ?? "", http.RequestAborted);
                return;
        }

        var original = http.Response.Body;
        await using var buffer = new MemoryStream();
        http.Response.Body = buffer;
        try
        {
            await next(http);
            var ok = http.Response.StatusCode is >= 200 and < 300;
            var body = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
            await using (var scope = Scoped(scopes, orgId, out var svc))
            {
                if (ok) await svc.CompleteAsync(begin.RecordId, http.Response.StatusCode, http.Response.ContentType, body, CancellationToken.None);
                else await svc.AbandonAsync(begin.RecordId, CancellationToken.None);
            }
        }
        catch
        {
            await using var scope = Scoped(scopes, orgId, out var svc);
            await svc.AbandonAsync(begin.RecordId, CancellationToken.None);
            throw;
        }
        finally
        {
            http.Response.Body = original;
        }
        buffer.Position = 0;
        await buffer.CopyToAsync(original, http.RequestAborted);
    }

    private static AsyncServiceScope Scoped(IServiceScopeFactory scopes, Guid orgId, out IdempotencyService svc)
    {
        var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IOrgContext>().Use(orgId);
        svc = scope.ServiceProvider.GetRequiredService<IdempotencyService>();
        return scope;
    }
}
