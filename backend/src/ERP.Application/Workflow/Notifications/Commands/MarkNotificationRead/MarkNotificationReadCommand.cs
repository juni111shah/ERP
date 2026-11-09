using ERP.Application.Common;

namespace ERP.Application.Workflow;

/// <summary>Marks one of the caller's notifications as read.</summary>
public sealed record MarkNotificationReadCommand(Guid Id) : ICommand;
