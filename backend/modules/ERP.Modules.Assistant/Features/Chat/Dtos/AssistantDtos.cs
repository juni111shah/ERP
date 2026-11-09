using System.Text.Json;
using ERP.Application.Assistant;
using ERP.Modules.Assistant.Domain;

namespace ERP.Modules.Assistant.Features;

public sealed record WorkspaceAssistantStatus(bool Available);
public sealed record WorkspaceMessageInput(Guid? ConversationId, string Text, string? Page);
public sealed record AssistantConversationRow(Guid Id, string Title, DateTime LastMessageAt);
public sealed record AssistantMessageRow(DateTime At, bool FromUser, string Text);

public sealed record AssistantActionCard(Guid Id, string Title, IReadOnlyList<AssistantActionLine> Lines, string Status, string? Message, string? Link, DateTime CreatedAt)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static AssistantActionCard From(AssistantPendingAction a)
        => new(a.Id, a.Title, JsonSerializer.Deserialize<List<AssistantActionLine>>(a.LinesJson, Json) ?? [], a.Status, a.Message, a.Link, a.CreatedAt);
}

public sealed record AssistantConversationDetail(Guid Id, string Title, IReadOnlyList<AssistantMessageRow> Messages, IReadOnlyList<AssistantActionCard> Actions);
