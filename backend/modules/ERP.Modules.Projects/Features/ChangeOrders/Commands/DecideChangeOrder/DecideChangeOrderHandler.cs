using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class DecideChangeOrderHandler(IProjectChangeOrderRepository changeOrders, IProjectRepository projects, ProjectAccess access) : ICommandHandler<DecideChangeOrderCommand, ChangeOrderDto>
{
    public async Task<ChangeOrderDto> Handle(DecideChangeOrderCommand command, CancellationToken ct)
    {
        // 1. Load it; only a draft on an open project can be decided.
        var c = await changeOrders.GetForUpdateAsync(command.Id, ct);
        await access.EnsureProjectAsync(c.ProjectId, ct);
        if (c.Status != ChangeOrderStatus.Draft) throw new ConflictException("This change order has already been decided.");
        ProjectRules.EnsureOpen(await projects.GetAsync(c.ProjectId, ct));

        // 2. Decide and save.
        c.Status = command.Approve ? ChangeOrderStatus.Approved : ChangeOrderStatus.Rejected;
        c.DecidedAt = ERP.Domain.Common.AppClock.UtcNow; c.DecisionNote = command.Note?.Trim();
        await changeOrders.SaveChangesAsync(ct);
        return ChangeOrderDto.From(c);
    }
}
