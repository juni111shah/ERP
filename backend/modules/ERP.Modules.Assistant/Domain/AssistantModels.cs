using ERP.Domain.Common;

namespace ERP.Modules.Assistant.Domain;

/// <summary>A person's chat with the assistant. It is private to that person, and it is not copied into the audit trail.</summary>
public class AssistantConversation : OrgEntity, INotAudited
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = default!;
    public DateTime StartedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
}

public class AssistantChatMessage : OrgEntity, INotAudited
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public DateTime At { get; set; }
    public bool FromUser { get; set; }
    /// <summary>What became of a suggestion, for the model's next turn. People see the card instead, so a note never appears in the chat.</summary>
    public bool IsNote { get; set; }
    public string Text { get; set; } = default!;
}

/// <summary>How many messages a person sent on a day. Kept apart from the chats, so deleting a chat cannot reset a limit. It holds no words.</summary>
public class AssistantMessageCount : OrgEntity, INotAudited
{
    public Guid UserId { get; set; }
    public DateOnly Day { get; set; }
    public int Count { get; set; }
}

/// <summary>A change the assistant suggested, waiting for that person's click. The change itself, once made, is audited as usual.</summary>
public class AssistantPendingAction : OrgEntity, INotAudited
{
    public const string Pending = "pending";
    public const string Done = "done";
    public const string Cancelled = "cancelled";
    public const string Failed = "failed";
    public const string Expired = "expired";

    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public string ActionKey { get; set; } = default!;
    public string ArgumentsJson { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string LinesJson { get; set; } = default!;
    public string Status { get; set; } = Pending;
    public string? Message { get; set; }
    public string? Link { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    /// <summary>The language the card was written in, which its outcome follows.</summary>
    public string Language { get; set; } = "en";
    /// <summary>The legal entity shown on the card, which Confirm runs against. Null for a change that belongs to none.</summary>
    public Guid? LegalEntityId { get; set; }
}
