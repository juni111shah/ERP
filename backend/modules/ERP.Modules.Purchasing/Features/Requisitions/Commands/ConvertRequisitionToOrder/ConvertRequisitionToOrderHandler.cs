using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class ConvertRequisitionToOrderHandler(
    IPurchaseRequisitionRepository requisitions,
    IPurchaseOrderRepository orders,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ICustomFieldService fields) : ICommandHandler<ConvertRequisitionToOrderCommand, PurchaseOrderDto>
{
    public async Task<PurchaseOrderDto> Handle(ConvertRequisitionToOrderCommand cmd, CancellationToken ct)
    {
        // 1. Only an approved requisition can be ordered.
        var req = await requisitions.FindWithLinesForUpdateAsync(cmd.Id, ct) ?? throw new NotFoundException($"Requisition '{cmd.Id}' not found.");

        PurchasingTransitions.Requisition.Ensure(req.Status, PurchaseRequisitionStatus.Ordered, isSystem: true);
        var entity = await guard.RequireAsync(req.LegalEntityId, ct);

        // 2. A draft order with the requisition's lines at the estimated prices.
        var order = new PurchaseOrder
        {
            LegalEntityId = req.LegalEntityId,
            RequisitionId = req.Id,
            VendorPartyId = cmd.VendorPartyId,
            VendorName = string.IsNullOrWhiteSpace(cmd.VendorName) ? "Unassigned Vendor" : cmd.VendorName.Trim(),
            OrderDate = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow),
            ExpectedDeliveryDate = req.RequiredDate,
            Currency = entity.BaseCurrency,
            Status = PurchaseOrderStatus.Draft,
            Notes = req.Reason,
            Number = await numbers.NextAsync(Docs.PurchaseOrder, req.LegalEntityId, DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), ct),
        };

        decimal subtotal = 0;
        foreach (var l in req.Lines)
        {
            var lineTotal = l.Quantity * l.EstimatedUnitPrice;
            subtotal += lineTotal;
            order.Lines.Add(new PurchaseOrderLine
            {
                ItemId = l.ItemId,
                ItemCode = l.ItemCode,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitOfMeasure = l.UnitOfMeasure,
                UnitPrice = l.EstimatedUnitPrice,
                LineTotal = lineTotal
            });
        }

        order.SubTotal = subtotal;
        order.TotalAmount = subtotal;

        // 3. Save the order and mark the requisition ordered.
        orders.Add(order);
        req.Status = PurchaseRequisitionStatus.Ordered;
        req.ConvertedPurchaseOrderId = order.Id;

        await requisitions.SaveChangesAsync(ct);
        await orders.SaveChangesAsync(ct);

        return await PurchaseOrderDto.FromAsync(order, fields, ct);
    }
}
