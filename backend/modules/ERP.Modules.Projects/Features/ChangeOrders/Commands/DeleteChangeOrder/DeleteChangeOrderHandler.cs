using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;
using MediatR;

namespace ERP.Modules.Projects.Features;

internal sealed class DeleteChangeOrderHandler(IProjectChangeOrderRepository changeOrders, ProjectAccess access) : ICommandHandler<DeleteChangeOrderCommand>
{
    public async Task<Unit> Handle(DeleteChangeOrderCommand command, CancellationToken ct)
    {
        var c = await changeOrders.GetForUpdateAsync(command.Id, ct);
        await access.EnsureProjectAsync(c.ProjectId, ct);
        if (c.Status != ChangeOrderStatus.Draft) throw new ConflictException("Only a draft change order can be deleted.");
        changeOrders.Remove(c);
        await changeOrders.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
