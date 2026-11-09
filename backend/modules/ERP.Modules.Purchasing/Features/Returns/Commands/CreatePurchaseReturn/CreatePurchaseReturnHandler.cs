using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class CreatePurchaseReturnHandler(
    IPurchaseReturnRepository returns,
    PurchaseReturnSources sources,
    IStockPostingService stock,
    ILegalEntityGuard guard,
    INumberingService numbers) : ICommandHandler<CreatePurchaseReturnCommand, PurchaseReturnDto>
{
    public async Task<PurchaseReturnDto> Handle(CreatePurchaseReturnCommand cmd, CancellationToken ct)
    {
        // 1. The order or bill, its legal entity and vendor, and a usable warehouse.
        var i = cmd.Input;
        var (order, bill) = await sources.SourceAsync(i.PurchaseOrderId, i.BillId, ct);
        var legalEntityId = order?.LegalEntityId ?? bill!.LegalEntityId;
        await guard.RequireAsync(legalEntityId, ct);
        var vendor = order?.VendorPartyId ?? bill?.PartyId ?? throw new ConflictException("This purchase order has no vendor party.");
        if (i.WarehouseId is { } warehouseId && !await stock.IsWarehouseUsableAsync(warehouseId, legalEntityId, ct))
            throw new FieldValidationException("warehouseId", "The warehouse was not found, is not active, or belongs to another legal entity.");

        // 2. The draft return, its lines taken from the order (or the bill).
        var ret = new PurchaseReturn
        {
            LegalEntityId = legalEntityId,
            PurchaseOrderId = order?.Id,
            PurchaseOrderNumber = order?.Number,
            BillId = bill?.Id,
            VendorPartyId = vendor,
            VendorName = order?.VendorName ?? bill!.PartyName ?? string.Empty,
            ReturnDate = i.ReturnDate,
            Reason = i.Reason.Trim(),
            Notes = i.Notes,
            WarehouseId = i.WarehouseId,
            Number = await numbers.NextAsync(Docs.PurchaseReturn, legalEntityId, i.ReturnDate, ct),
        };
        foreach (var l in i.Lines)
        {
            if (order is not null)
            {
                var line = order.Lines.FirstOrDefault(x => x.Id == l.OrderLineId) ?? throw new FieldValidationException("lines", "A line does not belong to this purchase order.");
                PurchaseReturnSources.EnsureWithinReceived(line, l.Quantity);
                ret.Lines.Add(new PurchaseReturnLine { OrderLineId = line.Id, ItemId = line.ItemId, ItemCode = line.ItemCode, Description = line.Description, Quantity = l.Quantity });
            }
            else
            {
                var line = bill!.Lines.FirstOrDefault(x => x.Id == l.BillLineId) ?? throw new FieldValidationException("lines", "A line does not belong to this bill.");
                ret.Lines.Add(new PurchaseReturnLine { BillLineId = line.Id, ItemId = line.ItemId, Description = line.Description, Quantity = l.Quantity });
            }
        }

        returns.Add(ret);
        await returns.SaveChangesAsync(ct);
        return PurchaseReturnDto.From(ret);
    }
}
