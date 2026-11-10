using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class UpdatePurchaseOrderHandler(
    IPurchaseOrderRepository orders,
    IRepository<PurchaseOrderLine> lineRows,
    PurchaseOrderRules rules,
    PurchasingApprovalGate approvalGate,
    ILegalEntityGuard guard,
    ICustomFieldService fields) : ICommandHandler<UpdatePurchaseOrderCommand, PurchaseOrderDto>
{
    public async Task<PurchaseOrderDto> Handle(UpdatePurchaseOrderCommand cmd, CancellationToken ct)
    {
        // 1. Only a draft or submitted order of the same legal entity can change.
        var order = await orders.FindWithLinesForUpdateAsync(cmd.Id, ct) ?? throw new NotFoundException($"Purchase order '{cmd.Id}' not found.");

        if (order.Status == PurchaseOrderStatus.Billed)
            throw new ConflictException("Cannot edit a billed purchase order.");
        if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted))
            throw new ConflictException("Only a draft or submitted purchase order can be edited.");

        var i = cmd.Input;
        if (i.LegalEntityId != order.LegalEntityId)
            throw new FieldValidationException("legalEntityId", "The legal entity of a purchase order cannot change.");

        // 2. The header.
        var entity = await guard.RequireAsync(order.LegalEntityId, ct);
        var vendorName = await rules.ValidateHeaderAsync(i, entity.BaseCurrency, ct);
        order.VendorPartyId = i.VendorPartyId;
        order.VendorName = vendorName;
        order.VendorContact = i.VendorContact?.Trim();
        order.VendorReference = i.VendorReference?.Trim();
        order.OrderDate = i.OrderDate;
        order.ExpectedDeliveryDate = i.ExpectedDeliveryDate;
        order.Currency = i.Currency.Trim().ToUpperInvariant();
        order.PaymentTerms = i.PaymentTerms;
        order.DeliveryAddress = i.DeliveryAddress;
        order.Notes = i.Notes;

        // 3. What was approved is no longer what the order says.
        await approvalGate.ReleaseAsync(order.ApprovalRequestId, ct);
        order.ApprovalRequestId = null;

        // 4. Lines are replaced through the line repository: new lines already carry keys, so adding them to the order's collection
        // would make EF treat them as existing rows (and the workspace guard then rejects the write).
        var fresh = PurchaseOrderRules.BuildLinesAndTotals(order, i.Lines);
        foreach (var old in order.Lines.ToList()) lineRows.Remove(old);
        lineRows.AddRange(fresh);
        await fields.ApplyAsync(Docs.PurchaseOrder, order, i.CustomFields, isCreate: false, ct);

        await orders.SaveChangesAsync(ct);
        order.Lines.Clear(); order.Lines.AddRange(fresh);
        return await PurchaseOrderDto.FromAsync(order, fields, ct);
    }
}
