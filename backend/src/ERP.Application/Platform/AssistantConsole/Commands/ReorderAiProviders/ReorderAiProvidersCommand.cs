using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Sets the order the AI services are tried in (owner only). The list must name every service.</summary>
public sealed record ReorderAiProvidersCommand(AiProviderOrderInput Input) : ICommand<AssistantConsoleView>;
