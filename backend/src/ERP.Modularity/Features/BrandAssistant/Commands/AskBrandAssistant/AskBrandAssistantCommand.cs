using ERP.Application.Assistant;
using MediatR;

namespace ERP.Modularity.Features;

/// <summary>A visitor's message to the public assistant. It reads public facts only: nothing here touches a workspace, even when the visitor is signed in.</summary>
public sealed record AskBrandAssistantCommand(BrandMessageInput Input) : IStreamRequest<AssistantEvent>;
