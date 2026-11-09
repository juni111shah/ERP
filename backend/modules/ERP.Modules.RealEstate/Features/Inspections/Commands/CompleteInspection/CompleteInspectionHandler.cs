using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CompleteInspectionHandler(
    IInspectionRepository inspections, IRepository<WorkOrder> workOrders, InspectionDtoFactory factory, INumberingService numbers, IUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<CompleteInspectionCommand, InspectionDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public Task<InspectionDto> Handle(CompleteInspectionCommand cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        // 1. Only an open inspection with a checklist can be completed.
        var i = await inspections.GetWithItemsForUpdateAsync(cmd.Id, token);
        if (i.Status == InspectionStatus.Completed) throw new ConflictException("This inspection is already completed.");
        if (i.Items.Count == 0) throw new ConflictException("Add at least one checklist item before completing.");

        // 2. Raise a repair work order for each poor or damaged item when asked.
        if (cmd.RaiseWorkOrders)
            foreach (var item in i.Items.Where(x => x.Condition >= ItemCondition.Poor && x.WorkOrderId is null))
            {
                var w = new WorkOrder
                {
                    LegalEntityId = i.LegalEntityId, PropertyId = i.PropertyId, UnitId = i.UnitId, LeaseId = i.LeaseId, Issue = $"{item.Area}: {item.Condition} (inspection)", Category = "Inspection",
                    Priority = item.Condition == ItemCondition.Damaged ? Priority.High : Priority.Normal, ReportedOn = Today, ReportedBy = "Inspection", EstimatedCost = item.RepairCost, Details = item.Notes,
                };
                w.Number = await numbers.NextAsync(Docs.WorkOrder, w.LegalEntityId, Today, token);
                workOrders.Add(w);
                item.WorkOrderId = w.Id;
            }

        // 3. Complete and save.
        i.Status = InspectionStatus.Completed;
        await inspections.SaveChangesAsync(token);
        return await factory.BuildAsync(i, token);
    }, ct);
}
