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

/// <summary>Re-runs <see cref="IRetryOnConcurrency"/> commands up to three times on <see cref="DbUpdateConcurrencyException"/> or a duplicate document number, with a short random pause.</summary>
public sealed class ConcurrencyRetryBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork, IEnumerable<IResetOnRetry> resets, IDbErrors dbErrors, ILogger<ConcurrencyRetryBehavior<TRequest, TResponse>> log, IOrgContext? org = null)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public const int MaxAttempts = 3;

    // One retryable command at a time per workspace and process: concurrent requests would all allocate the same next number and
    // three optimistic attempts cannot clear a queue of twenty. Several instances still rely on the optimistic retry below.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IRetryOnConcurrency) return await next();
        var gate = Gates.GetOrAdd(org?.OrgId ?? Guid.Empty, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try { return await RunAsync(next, ct); }
        finally { gate.Release(); }
    }

    private async Task<TResponse> RunAsync(RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { return await next(); }
            // Two requests that allocate the same next document number either trip the row version or, on first use of a
            // sequence, the unique index on the document number: both mean "someone else got there first".
            catch (DbUpdateException ex) when (attempt < MaxAttempts && (ex is DbUpdateConcurrencyException || dbErrors.IsUniqueViolation(ex, "_Number")))
            {
                log.LogInformation("{Request} lost a concurrency race (attempt {Attempt} of {Max}); retrying", typeof(TRequest).Name, attempt, MaxAttempts);
                unitOfWork.DiscardChanges();
                foreach (var r in resets) r.ResetForRetry();
                await Task.Delay(Random.Shared.Next(25, 100), ct);
            }
        }
    }
}
