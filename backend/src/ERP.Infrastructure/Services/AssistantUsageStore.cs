using ERP.Application.Assistant;
using ERP.Domain.Platform;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public sealed class AssistantUsageStore(AppDbContext db) : IAssistantUsageStore
{
    private const int MaxFailureLength = 200;
    private const int FailuresBeforeAlert = 3;

    public async Task<long> TokensOnAsync(DateOnly day, AssistantBot bot, CancellationToken ct = default)
    {
        var name = bot.ToString();
        return await db.AssistantUsageDays.AsNoTracking().Where(u => u.Day == day && u.Bot == name).SumAsync(u => u.InputTokens + u.OutputTokens, ct);
    }

    public async Task RecordAsync(DateOnly day, AssistantBot bot, Guid providerId, string providerName, int inputTokens, int outputTokens, string? failure, CancellationToken ct = default)
    {
        if (await UpdateAsync(day, bot, providerId, providerName, inputTokens, outputTokens, failure, ct) > 0) return;
        var row = new AssistantUsageDay
        {
            Day = day, Bot = bot.ToString(), ProviderId = providerId, ProviderName = providerName, Requests = failure is null ? 1 : 0, Failures = failure is null ? 0 : 1,
            InputTokens = inputTokens, OutputTokens = outputTokens, LastFailure = Short(failure),
        };
        db.AssistantUsageDays.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
            db.Entry(row).State = EntityState.Detached;
        }
        catch (DbUpdateException)
        {
            // Another reply created today's row first, so add to that one.
            db.Entry(row).State = EntityState.Detached;
            await UpdateAsync(day, bot, providerId, providerName, inputTokens, outputTokens, failure, ct);
        }
    }

    public async Task<IReadOnlyList<AssistantUsageRow>> ListAsync(DateOnly day, CancellationToken ct = default)
    {
        var rows = await db.AssistantUsageDays.AsNoTracking().Where(u => u.Day == day).OrderBy(u => u.Bot).ThenBy(u => u.ProviderName).ToListAsync(ct);
        return rows.Select(u => new AssistantUsageRow(Enum.Parse<AssistantBot>(u.Bot), u.ProviderId, u.ProviderName, u.Requests, u.Failures, u.InputTokens, u.OutputTokens, u.LastFailure)).ToList();
    }

    public async Task<IReadOnlyList<AssistantUsageRow>> TotalsSinceAsync(DateOnly from, CancellationToken ct = default)
    {
        var rows = await db.AssistantUsageDays.AsNoTracking().Where(u => u.Day >= from).OrderBy(u => u.Day).ToListAsync(ct);
        return rows.GroupBy(u => (u.Bot, u.ProviderId)).Select(g => new AssistantUsageRow(
            Enum.Parse<AssistantBot>(g.Key.Bot), g.Key.ProviderId, g.Last().ProviderName, g.Sum(u => u.Requests), g.Sum(u => u.Failures), g.Sum(u => u.InputTokens), g.Sum(u => u.OutputTokens), g.LastOrDefault(u => u.LastFailure != null)?.LastFailure))
            .OrderBy(u => u.Bot).ThenBy(u => u.ProviderName).ToList();
    }

    public async Task ServiceFailedAsync(Guid providerId, string failure, bool atOnce, DateTime at, CancellationToken ct = default)
    {
        await db.AiProviders.Where(p => p.Id == providerId).ExecuteUpdateAsync(s => s.SetProperty(p => p.ConsecutiveFailures, p => p.ConsecutiveFailures + 1), ct);
        await db.AiProviders.Where(p => p.Id == providerId && (atOnce || p.ConsecutiveFailures >= FailuresBeforeAlert)).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.LastTestOk, (bool?)false).SetProperty(p => p.LastTestMessage, Short(failure)).SetProperty(p => p.LastTestedAt, (DateTime?)at), ct);
    }

    public Task ServiceWorkedAsync(Guid providerId, DateTime at, CancellationToken ct = default)
        => db.AiProviders.Where(p => p.Id == providerId).ExecuteUpdateAsync(s => s.SetProperty(p => p.ConsecutiveFailures, 0)
            .SetProperty(p => p.LastTestOk, (bool?)true).SetProperty(p => p.LastTestMessage, "It answered a real question.").SetProperty(p => p.LastTestedAt, (DateTime?)at), ct);

    private Task<int> UpdateAsync(DateOnly day, AssistantBot bot, Guid providerId, string providerName, int inputTokens, int outputTokens, string? failure, CancellationToken ct)
    {
        var name = bot.ToString();
        var query = db.AssistantUsageDays.Where(u => u.Day == day && u.Bot == name && u.ProviderId == providerId);
        return failure is null
            ? query.ExecuteUpdateAsync(s => s.SetProperty(u => u.ProviderName, providerName).SetProperty(u => u.Requests, u => u.Requests + 1)
                .SetProperty(u => u.InputTokens, u => u.InputTokens + inputTokens).SetProperty(u => u.OutputTokens, u => u.OutputTokens + outputTokens), ct)
            : query.ExecuteUpdateAsync(s => s.SetProperty(u => u.ProviderName, providerName).SetProperty(u => u.Failures, u => u.Failures + 1)
                .SetProperty(u => u.InputTokens, u => u.InputTokens + inputTokens).SetProperty(u => u.OutputTokens, u => u.OutputTokens + outputTokens)
                .SetProperty(u => u.LastFailure, Short(failure)), ct);
    }

    private static string? Short(string? failure) => failure is { Length: > MaxFailureLength } ? failure[..MaxFailureLength] : failure;
}
