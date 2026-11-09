using ERP.Application.Common;

namespace ERP.Modules.Assistant.Features;

/// <summary>Declines a change the assistant suggested while it is still pending.</summary>
public sealed record CancelAssistantActionCommand(Guid Id) : ICommand<AssistantActionCard>;
