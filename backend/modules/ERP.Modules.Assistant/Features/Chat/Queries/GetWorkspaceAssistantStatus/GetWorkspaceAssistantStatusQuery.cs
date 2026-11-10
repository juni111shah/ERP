using ERP.Application.Common;

namespace ERP.Modules.Assistant.Features;

/// <summary>Whether the assistant can answer in this workspace: shown by the platform owner and a service cleared for workspaces.</summary>
public sealed record GetWorkspaceAssistantStatusQuery : IQuery<WorkspaceAssistantStatus>;
