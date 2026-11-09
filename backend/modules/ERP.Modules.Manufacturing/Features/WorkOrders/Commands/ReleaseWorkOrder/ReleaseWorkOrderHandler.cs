using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class ReleaseWorkOrderHandler(
    IWorkOrderRepository orders,
    IBillOfMaterialsRepository boms,
    WorkOrderMaterials materials,
    ISettingsService settings,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReleaseWorkOrderCommand, WorkOrderDto>
{
    public Task<WorkOrderDto> Handle(ReleaseWorkOrderCommand request, CancellationToken cancellationToken) => unitOfWork.InTransactionAsync(async token =>
    {
        // 1. Move it to in progress.
        var wo = await orders.GetWithDetailsForUpdateAsync(request.Id, token);
        ManufacturingTransitions.WorkOrder.Ensure(wo.Status, WorkOrderStatus.InProgress, isSystem: false);
        wo.Status = WorkOrderStatus.InProgress;

        // 2. Issue the BOM when asked, or when the setting says so.
        var issue = request.IssueMaterials ?? await settings.GetAsync<bool?>("manufacturing.issueMaterialsOnRelease", wo.LegalEntityId, token) ?? false;
        if (issue)
        {
            var bom = await boms.GetWithComponentsAsync(wo.BomId, token);
            await materials.IssueAsync(wo, await materials.BomNeedsAsync(wo, bom, wo.PlannedQuantity, token), WorkOrderRules.Today, backflushed: false, $"Issued on release of {wo.WorkOrderNumber}", token);
        }

        // 3. Save.
        await orders.SaveChangesAsync(token);
        return WorkOrderDto.From(wo);
    }, cancellationToken);
}
