using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class UpdateChangeOrderHandler(IProjectChangeOrderRepository changeOrders, ProjectAccess access) : ICommandHandler<UpdateChangeOrderCommand, ChangeOrderDto>
{
    public async Task<ChangeOrderDto> Handle(UpdateChangeOrderCommand command, CancellationToken ct)
    {
        var c = await changeOrders.GetForUpdateAsync(command.Id, ct);
        await access.EnsureProjectAsync(c.ProjectId, ct);
        if (c.Status != ChangeOrderStatus.Draft) throw new ConflictException("Only a draft change order can be edited.");
        command.Input.ApplyTo(c);
        await changeOrders.SaveChangesAsync(ct);
        return ChangeOrderDto.From(c);
    }
}
