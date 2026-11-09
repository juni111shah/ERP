namespace ERP.Application.Workflow;

public sealed record NotificationDto(Guid Id, string Title, string? Body, string? Link, string Severity, DateTime CreatedAt, DateTime? ReadAt);
public sealed record NotificationList(int Unread, IReadOnlyList<NotificationDto> Items);
