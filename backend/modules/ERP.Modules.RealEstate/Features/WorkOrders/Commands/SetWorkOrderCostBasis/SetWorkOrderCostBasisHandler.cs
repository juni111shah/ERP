using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class SetWorkOrderCostBasisHandler(IRepository<WorkOrder> workOrders, IRealEstateBillRepository bills, ICustomFieldService fields)
    : ICommandHandler<SetWorkOrderCostBasisCommand, WorkOrderDto>
{
    public async Task<WorkOrderDto> Handle(SetWorkOrderCostBasisCommand cmd, CancellationToken ct)
    {
        // 1. A linked bill must be posted and of the same legal entity.
        var w = await workOrders.GetForUpdateAsync(cmd.Id, ct);
        var i = cmd.Input;
        if (i.BillId is { } billId)
        {
            var bill = await bills.FindStateAsync(billId, ct)
                ?? throw new FieldValidationException("billId", "Bill not found.");
            if (bill.Status is ERP.Domain.Finance.DocumentStatus.Draft or ERP.Domain.Finance.DocumentStatus.PendingApproval or ERP.Domain.Finance.DocumentStatus.Voided)
                throw new FieldValidationException("billId", "Link a posted bill (drafts and voided bills carry no cost).");
            if (bill.LegalEntityId != w.LegalEntityId) throw new FieldValidationException("billId", "The bill belongs to another legal entity.");
        }

        // 2. Who bears it: the owner's share follows from the responsibility.
        w.ChargeableTo = i.ChargeableTo;
        w.OwnerSharePercent = i.ChargeableTo == CostResponsibility.Shared ? i.OwnerSharePercent!.Value : i.ChargeableTo is CostResponsibility.Company or CostResponsibility.Tenant ? 0m : 100m;
        w.BillId = i.BillId;

        // 3. Save.
        await workOrders.SaveChangesAsync(ct);
        return WorkOrderDto.From(w, await fields.ReadAsync(Docs.WorkOrder, w, ct));
    }
}
