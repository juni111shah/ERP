using ERP.Application.Abstractions;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Assistant.Repositories;

internal sealed class WorkspaceAssistantSettingRepository(IRepository<PlatformSetting> inner) : RepositoryBase<PlatformSetting>(inner), IWorkspaceAssistantSettingRepository
{
    public Task<bool> IsWorkspaceAssistantEnabledAsync(CancellationToken ct)
        => Query().OrderBy(s => s.CreatedAt).Select(s => s.WorkspaceAssistantEnabled).FirstOrDefaultAsync(ct);
}
