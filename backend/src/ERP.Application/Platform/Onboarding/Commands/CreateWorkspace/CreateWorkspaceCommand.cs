using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The older sign-up route, on only where a deployment switches it on. It starts on the default plan like any registration, so it is no way round billing.</summary>
public sealed record CreateWorkspaceCommand(OnboardingRequest Request) : ICommand<OnboardingResult>;
