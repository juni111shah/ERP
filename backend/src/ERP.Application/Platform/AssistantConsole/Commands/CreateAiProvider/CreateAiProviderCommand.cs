using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Connects an AI service (owner only). The key is stored encrypted and never shown again.</summary>
public sealed record CreateAiProviderCommand(AiProviderInput Input) : ICommand<AssistantConsoleView>;
