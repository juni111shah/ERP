using ERP.Modules.Sales.Domain;

namespace ERP.Modules.Sales.Features;

public sealed record SalesReturnLineDto(Guid Id, Guid? OrderLineId, Guid? InvoiceLineId, Guid? ItemId, string ItemCode, string Description, decimal Quantity, bool Restock);
public sealed record SalesReturnDto(
    Guid Id, string Number, Guid LegalEntityId, Guid? SalesOrderId, string? OrderNumber, Guid? InvoiceId, string? InvoiceNumber, Guid CustomerPartyId, string CustomerName,
    DateOnly ReturnDate, SalesReturnStatus Status, string Reason, string? Notes, Guid? CreditNoteId, string? CreditNoteNumber, decimal SubTotal, decimal TaxTotal, decimal TotalAmount,
    IReadOnlyList<SalesReturnLineDto> Lines, string? VoidReason, DateTime? VoidedAt, DateTime CreatedAt)
{
    internal static SalesReturnDto From(SalesReturn r) => new(
        r.Id, r.Number, r.LegalEntityId, r.SalesOrderId, r.OrderNumber, r.InvoiceId, r.InvoiceNumber, r.CustomerPartyId, r.CustomerName, r.ReturnDate, r.Status, r.Reason, r.Notes,
        r.CreditNoteId, r.CreditNoteNumber, r.SubTotal, r.TaxTotal, r.TotalAmount,
        r.Lines.Select(l => new SalesReturnLineDto(l.Id, l.OrderLineId, l.InvoiceLineId, l.ItemId, l.ItemCode, l.Description, l.Quantity, l.Restock)).ToList(),
        r.VoidReason, r.VoidedAt, r.CreatedAt);
}
