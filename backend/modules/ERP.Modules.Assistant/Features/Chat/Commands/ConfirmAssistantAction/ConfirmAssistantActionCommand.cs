using ERP.Application.Common;

namespace ERP.Modules.Assistant.Features;

/// <summary>Makes a change the assistant suggested, on the caller's click; the card records how it went.</summary>
public sealed record ConfirmAssistantActionCommand(Guid Id) : ICommand<AssistantActionCard>;
