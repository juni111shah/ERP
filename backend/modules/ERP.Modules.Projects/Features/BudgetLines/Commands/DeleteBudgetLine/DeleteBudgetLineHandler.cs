using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;
using MediatR;

namespace ERP.Modules.Projects.Features;

internal sealed class DeleteBudgetLineHandler(IProjectBudgetLineRepository budgetLines, ProjectAccess access) : ICommandHandler<DeleteBudgetLineCommand>
{
    public async Task<Unit> Handle(DeleteBudgetLineCommand command, CancellationToken ct)
    {
        var line = await budgetLines.GetForUpdateAsync(command.Id, ct);
        await access.EnsureProjectAsync(line.ProjectId, ct);
        budgetLines.Remove(line);
        await budgetLines.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
