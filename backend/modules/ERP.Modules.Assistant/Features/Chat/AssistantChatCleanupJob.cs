using ERP.Application.Abstractions;
using ERP.Modules.Assistant.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Assistant.Features;

/// <summary>Deletes chats past the workspace's retention period. Changes people confirmed stay in the audit trail, which has its own rules.</summary>
internal sealed class AssistantChatCleanupJob(
    IRepository<AssistantConversation> conversations, IRepository<AssistantChatMessage> messages, IRepository<AssistantPendingAction> actions, ISettingsService settings) : IScheduledJob
{
    public string Name => "assistant-chat-cleanup";
    public string? ModuleKey => AssistantModule.Key;

    public async Task RunAsync(DateOnly today, CancellationToken ct)
    {
        var days = await settings.GetAsync<int?>(AssistantModule.RetentionDaysSetting, null, ct) ?? 90;
        var cutoff = today.AddDays(-Math.Max(days, 0)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var old = await conversations.Tracked().Where(c => c.LastMessageAt < cutoff).Take(1000).ToListAsync(ct);
        if (old.Count == 0) return;
        var ids = old.Select(c => c.Id).ToList();
        foreach (var m in await messages.Tracked().Where(m => ids.Contains(m.ConversationId)).ToListAsync(ct)) messages.Remove(m);
        foreach (var a in await actions.Tracked().Where(a => ids.Contains(a.ConversationId)).ToListAsync(ct)) actions.Remove(a);
        foreach (var c in old) conversations.Remove(c);
        await conversations.SaveChangesAsync(ct);
    }
}
