using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class CreatePurchaseOrderHandler(
    IPurchaseOrderRepository orders,
    PurchaseOrderRules rules,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ICustomFieldService fields) : ICommandHandler<CreatePurchaseOrderCommand, PurchaseOrderDto>
{
    public async Task<PurchaseOrderDto> Handle(CreatePurchaseOrderCommand cmd, CancellationToken ct)
    {
        // 1. The header: legal entity, vendor and currency.
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var vendorName = await rules.ValidateHeaderAsync(i, entity.BaseCurrency, ct);
        var order = new PurchaseOrder
        {
            LegalEntityId = entity.Id,
            RequisitionId = i.RequisitionId,
            VendorPartyId = i.VendorPartyId,
            VendorName = vendorName,
            VendorContact = i.VendorContact?.Trim(),
            VendorReference = i.VendorReference?.Trim(),
            OrderDate = i.OrderDate,
            ExpectedDeliveryDate = i.ExpectedDeliveryDate,
            Currency = i.Currency.Trim().ToUpperInvariant(),
            PaymentTerms = i.PaymentTerms,
            DeliveryAddress = i.DeliveryAddress,
            Notes = i.Notes,
            Status = PurchaseOrderStatus.Draft,
        };

        // 2. A free number (the next in the sequence unless one is given).
        order.Number = string.IsNullOrWhiteSpace(i.Number)
            ? await numbers.NextAsync(Docs.PurchaseOrder, entity.Id, i.OrderDate, ct)
            : i.Number.Trim();

        if (await orders.AnyAsync(x => x.Number == order.Number, ct))
            throw new ConflictException($"Purchase order number '{order.Number}' already exists.");

        // 3. Lines, totals and custom fields, then save.
        order.Lines.AddRange(PurchaseOrderRules.BuildLinesAndTotals(order, i.Lines));
        await fields.ApplyAsync(Docs.PurchaseOrder, order, i.CustomFields, isCreate: true, ct);

        orders.Add(order);
        await orders.SaveChangesAsync(ct);
        return await PurchaseOrderDto.FromAsync(order, fields, ct);
    }
}
