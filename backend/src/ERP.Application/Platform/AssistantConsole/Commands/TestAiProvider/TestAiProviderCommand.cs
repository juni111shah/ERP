using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Makes one small real call to a connected AI service and keeps the result (owner only).</summary>
public sealed record TestAiProviderCommand(Guid Id) : ICommand<AssistantConsoleView>;
