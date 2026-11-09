using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Features;

public sealed record PurchaseReturnLineDto(Guid Id, Guid? OrderLineId, Guid? BillLineId, Guid? ItemId, string ItemCode, string Description, decimal Quantity);
public sealed record PurchaseReturnBillShareDto(Guid BillId, decimal Quantity, decimal Amount, decimal AppliedToBill);
public sealed record PurchaseReturnDto(
    Guid Id, string Number, Guid LegalEntityId, Guid? PurchaseOrderId, string? PurchaseOrderNumber, Guid? BillId, Guid VendorPartyId, string VendorName,
    DateOnly ReturnDate, PurchaseReturnStatus Status, string Reason, string? Notes, Guid? WarehouseId, decimal SubTotal, decimal TaxTotal, decimal TotalAmount, decimal StockValue, Guid? JournalEntryId,
    IReadOnlyList<PurchaseReturnLineDto> Lines, IReadOnlyList<PurchaseReturnBillShareDto> BillShares, string? VoidReason, DateTime? VoidedAt, DateTime CreatedAt)
{
    internal static PurchaseReturnDto From(PurchaseReturn r) => new(
        r.Id, r.Number, r.LegalEntityId, r.PurchaseOrderId, r.PurchaseOrderNumber, r.BillId, r.VendorPartyId, r.VendorName, r.ReturnDate, r.Status, r.Reason, r.Notes, r.WarehouseId,
        r.SubTotal, r.TaxTotal, r.TotalAmount, r.StockValue, r.JournalEntryId,
        r.Lines.Select(l => new PurchaseReturnLineDto(l.Id, l.OrderLineId, l.BillLineId, l.ItemId, l.ItemCode, l.Description, l.Quantity)).ToList(),
        r.BillShares.Select(s => new PurchaseReturnBillShareDto(s.BillId, s.Quantity, s.Amount, s.AppliedToBill)).ToList(),
        r.VoidReason, r.VoidedAt, r.CreatedAt);
}
