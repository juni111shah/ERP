using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;
using MediatR;

namespace ERP.Modules.Projects.Features;

internal sealed class DeleteRiskHandler(IProjectRiskRepository risks, ProjectAccess access) : ICommandHandler<DeleteRiskCommand>
{
    public async Task<Unit> Handle(DeleteRiskCommand command, CancellationToken ct)
    {
        var risk = await risks.GetForUpdateAsync(command.Id, ct);
        await access.EnsureProjectAsync(risk.ProjectId, ct);
        risks.Remove(risk);
        await risks.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
