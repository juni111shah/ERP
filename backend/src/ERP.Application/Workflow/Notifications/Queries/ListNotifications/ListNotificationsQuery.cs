using ERP.Application.Common;

namespace ERP.Application.Workflow;

/// <summary>The caller's latest notifications and unread count.</summary>
public sealed record ListNotificationsQuery : IQuery<NotificationList>;
