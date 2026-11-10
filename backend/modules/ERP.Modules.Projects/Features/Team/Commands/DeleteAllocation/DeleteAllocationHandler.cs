using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;
using MediatR;

namespace ERP.Modules.Projects.Features;

internal sealed class DeleteAllocationHandler(IProjectAllocationRepository allocations, ProjectAccess access) : ICommandHandler<DeleteAllocationCommand>
{
    public async Task<Unit> Handle(DeleteAllocationCommand command, CancellationToken ct)
    {
        var allocation = await allocations.GetForUpdateAsync(command.Id, ct);
        await access.EnsureProjectAsync(allocation.ProjectId, ct);
        allocations.Remove(allocation);
        await allocations.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
