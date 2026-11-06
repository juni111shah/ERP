using ERP.Domain.Common;

namespace ERP.Domain.Platform;

/// <summary>A chat with a visitor on the public pages. It belongs to no workspace, and holds no internet address or sign-in.</summary>
public class BrandConversation : Entity
{
    public DateTime StartedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public int VisitorMessages { get; set; }
    /// <summary>A reply quoted a price that matches no plan, so a person should read this one.</summary>
    public bool NeedsReview { get; set; }
}

public class BrandMessage : Entity
{
    public Guid ConversationId { get; set; }
    public DateTime At { get; set; }
    public bool FromVisitor { get; set; }
    public string Text { get; set; } = default!;
    /// <summary>The visitor's thumbs up or down on a reply. Null until they choose.</summary>
    public bool? Helpful { get; set; }
}

public enum BrandLeadStatus { New, Contacted, Won, Lost }

/// <summary>A visitor who asked to be contacted, in the form they filled in themselves.</summary>
public class BrandLead : Entity, IAuditable
{
    public Guid? ConversationId { get; set; }
    public BrandLeadStatus Status { get; set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Company { get; set; }
    public string? Need { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
