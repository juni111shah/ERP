using ERP.Application.Common;

namespace ERP.Application.Workflow;

/// <summary>Marks all of the caller's unread notifications as read.</summary>
public sealed record MarkAllNotificationsReadCommand : ICommand;
