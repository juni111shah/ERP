using ERP.Application.Abstractions;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Platform;

/// <summary>Work that belongs to the platform rather than to a workspace. It runs once on each pass of the scheduler.</summary>
public interface IPlatformJob
{
    Task RunAsync(DateOnly today, CancellationToken ct);
}

/// <summary>Deletes public assistant chats after 30 days. Leads are kept until the founder deletes them.</summary>
internal sealed class BrandConversationCleanupJob(IRepository<BrandConversation> conversations, IRepository<BrandMessage> messages) : IPlatformJob
{
    public const int RetentionDays = 30;

    public async Task RunAsync(DateOnly today, CancellationToken ct)
    {
        var cutoff = today.AddDays(-RetentionDays).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var old = await conversations.Tracked().Where(c => c.LastMessageAt < cutoff).Take(1000).ToListAsync(ct);
        if (old.Count == 0) return;
        var ids = old.Select(c => c.Id).ToList();
        foreach (var m in await messages.Tracked().Where(m => ids.Contains(m.ConversationId)).ToListAsync(ct)) messages.Remove(m);
        foreach (var c in old) conversations.Remove(c);
        await conversations.SaveChangesAsync(ct);
    }
}
