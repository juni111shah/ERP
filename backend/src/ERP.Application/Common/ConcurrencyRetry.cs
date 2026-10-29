using ERP.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Common;

/// <summary>
/// Marks a command that is safe to run again when its save loses a concurrency race (a document number or an invoice balance
/// was changed by someone else). Only mark commands that commit once: if a handler commits work and then fails later, a retry
/// would repeat the committed part.
/// </summary>
public interface IRetryOnConcurrency { }

/// <summary>Scoped state that must be thrown away before a retried command runs again (for example sequences created but never saved).</summary>
public interface IResetOnRetry { void ResetForRetry(); }

/// <summary>Re-runs <see cref="IRetryOnConcurrency"/> commands up to three times on <see cref="DbUpdateConcurrencyException"/>, with a short random pause.</summary>
public sealed class ConcurrencyRetryBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork, IEnumerable<IResetOnRetry> resets, ILogger<ConcurrencyRetryBehavior<TRequest, TResponse>> log)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public const int MaxAttempts = 3;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IRetryOnConcurrency) return await next();
        for (var attempt = 1; ; attempt++)
        {
            try { return await next(); }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                log.LogInformation("{Request} lost a concurrency race (attempt {Attempt} of {Max}); retrying", typeof(TRequest).Name, attempt, MaxAttempts);
                unitOfWork.DiscardChanges();
                foreach (var r in resets) r.ResetForRetry();
                await Task.Delay(Random.Shared.Next(25, 100), ct);
            }
        }
    }
}
