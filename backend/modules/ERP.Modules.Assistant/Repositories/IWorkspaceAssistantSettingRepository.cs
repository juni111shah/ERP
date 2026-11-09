using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Modules.Assistant.Repositories;

/// <summary>Reads the platform setting that shows or hides the workspace assistant.</summary>
public interface IWorkspaceAssistantSettingRepository : IRepository<PlatformSetting>
{
    /// <summary>Whether the platform owner shows the assistant in workspaces (the first settings row; false when there is none).</summary>
    Task<bool> IsWorkspaceAssistantEnabledAsync(CancellationToken ct);
}
