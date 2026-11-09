using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Changes a connected AI service (owner only). Leave the key empty to keep it; a new address needs the key again.</summary>
public sealed record UpdateAiProviderCommand(Guid Id, AiProviderInput Input) : ICommand<AssistantConsoleView>;
