using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;
using ERP.Modules.Assistant.Repositories;

namespace ERP.Modules.Assistant.Features;

internal sealed class GetWorkspaceAssistantStatusHandler(IWorkspaceAssistantSettingRepository settings, IReadRepository<AiProvider> providers)
    : IQueryHandler<GetWorkspaceAssistantStatusQuery, WorkspaceAssistantStatus>
{
    public async Task<WorkspaceAssistantStatus> Handle(GetWorkspaceAssistantStatusQuery q, CancellationToken ct)
    {
        var enabled = await settings.IsWorkspaceAssistantEnabledAsync(ct);
        return new(enabled && await providers.AnyAsync(p => p.UseForWorkspace, ct));
    }
}
