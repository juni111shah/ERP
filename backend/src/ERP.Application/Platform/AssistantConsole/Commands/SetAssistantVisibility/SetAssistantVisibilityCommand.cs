using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Shows or hides the public and the workspace assistant (owner only). Each needs a suitable service first.</summary>
public sealed record SetAssistantVisibilityCommand(AssistantVisibilityInput Input) : ICommand<AssistantConsoleView>;
