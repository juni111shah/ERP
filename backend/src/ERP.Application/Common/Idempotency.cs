using ERP.Application.Abstractions;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Common;

public enum IdempotencyOutcome { Started, Replay, InProgress, Mismatch }

public sealed record IdempotencyBegin(IdempotencyOutcome Outcome, Guid RecordId, int StatusCode = 0, string? ContentType = null, string? Body = null);

/// <summary>
/// Backs the <c>Idempotency-Key</c> header: the first request with a key claims it (unique per workspace, user, route and key),
/// later requests get the stored response. Failed responses release the key so the client can retry.
/// </summary>
public sealed class IdempotencyService(IRepository<IdempotencyRecord> records, IOrgContext org, IClock clock)
{
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    public const int MaxKeyLength = 100;

    public async Task<IdempotencyBegin> BeginAsync(Guid userId, string key, string route, string requestHash, CancellationToken ct)
    {
        var record = new IdempotencyRecord
        {
            OrgId = org.OrgId ?? throw new InvalidOperationException("No workspace in context."),
            UserId = userId, Key = key, Route = route, RequestHash = requestHash, CreatedAt = clock.UtcNow,
        };
        records.Add(record);
        try
        {
            await records.SaveChangesAsync(ct);
            return new IdempotencyBegin(IdempotencyOutcome.Started, record.Id);
        }
        catch (DbUpdateException)
        {
            records.DiscardChanges();
            var existing = await records.Query(new Specification<IdempotencyRecord>(r => r.UserId == userId && r.Key == key && r.Route == route)).FirstOrDefaultAsync(ct);
            if (existing is null) throw;
            if (existing.RequestHash != requestHash) return new IdempotencyBegin(IdempotencyOutcome.Mismatch, existing.Id);
            return existing.StatusCode == 0
                ? new IdempotencyBegin(IdempotencyOutcome.InProgress, existing.Id)
                : new IdempotencyBegin(IdempotencyOutcome.Replay, existing.Id, existing.StatusCode, existing.ContentType, existing.ResponseBody);
        }
    }

    public async Task CompleteAsync(Guid id, int statusCode, string? contentType, string body, CancellationToken ct)
    {
        var r = await records.GetForUpdateAsync(id, ct);
        r.StatusCode = statusCode; r.ContentType = contentType; r.ResponseBody = body;
        await records.SaveChangesAsync(ct);
    }

    /// <summary>Releases the key after a failed attempt so the client can send it again.</summary>
    public async Task AbandonAsync(Guid id, CancellationToken ct)
    {
        var r = await records.FindAsync(id, ct);
        if (r is null) return;
        records.Remove(await records.GetForUpdateAsync(id, ct));
        await records.SaveChangesAsync(ct);
    }
}

/// <summary>Nightly cleanup of expired idempotency records.</summary>
public sealed class IdempotencyCleanupJob(IRepository<IdempotencyRecord> records, IClock clock) : IScheduledJob
{
    public string Name => "core.idempotency-cleanup";
    public string? ModuleKey => null;

    public async Task RunAsync(DateOnly today, CancellationToken ct)
    {
        var cutoff = clock.UtcNow - IdempotencyService.Retention;
        var old = await records.Tracked(new Specification<IdempotencyRecord>(r => r.CreatedAt < cutoff)).Take(1000).ToListAsync(ct);
        if (old.Count == 0) return;
        foreach (var r in old) records.Remove(r);
        await records.SaveChangesAsync(ct);
    }
}
