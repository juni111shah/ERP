using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class CreateChangeOrderHandler(IProjectChangeOrderRepository changeOrders, IProjectRepository projects, ProjectAccess access) : ICommandHandler<CreateChangeOrderCommand, ChangeOrderDto>
{
    public async Task<ChangeOrderDto> Handle(CreateChangeOrderCommand command, CancellationToken ct)
    {
        // 1. The project must be open.
        var i = command.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        ProjectRules.EnsureOpen(p);
        await access.EnsureAsync(p, ct);

        // 2. numbered after the highest one so far: a deleted draft must not make the next number repeat
        var used = await changeOrders.ListNumbersOfProjectAsync(p.Id, ct);
        var next = used.Select(n => int.TryParse(n[(n.LastIndexOf('-') + 1)..], out var v) ? v : 0).DefaultIfEmpty(0).Max() + 1;

        // 3. Fill it in and save.
        var c = new ProjectChangeOrder { ProjectId = p.Id, Number = $"CO-{p.Number}-{next:00}" };
        i.ApplyTo(c);
        changeOrders.Add(c);
        await changeOrders.SaveChangesAsync(ct);
        return ChangeOrderDto.From(c);
    }
}
