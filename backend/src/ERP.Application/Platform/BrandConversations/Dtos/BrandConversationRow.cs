namespace ERP.Application.Platform;

public sealed record BrandConversationRow(Guid Id, DateTime StartedAt, DateTime LastMessageAt, int VisitorMessages, bool NeedsReview, string? FirstMessage);
public sealed record BrandMessageRow(DateTime At, bool FromVisitor, string Text);
public sealed record BrandAssistantStatus(bool Visible, string? SupportEmail);
