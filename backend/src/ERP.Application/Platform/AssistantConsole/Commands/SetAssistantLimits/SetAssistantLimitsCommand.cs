using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Sets how many tokens a day each assistant may use (owner only).</summary>
public sealed record SetAssistantLimitsCommand(AssistantLimitsInput Input) : ICommand<AssistantConsoleView>;
