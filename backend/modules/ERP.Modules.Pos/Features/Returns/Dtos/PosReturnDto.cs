using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

public record PosReturnLineDto(
    Guid Id,
    Guid OrderLineId,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate,
    decimal NetAmount,
    decimal TaxAmount,
    decimal LineTotal,
    bool Restock);

public record PosReturnRefundDto(Guid Id, PosPaymentMethod Method, decimal Amount, string? Reference);

public record PosReturnDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid OriginalOrderId,
    string OriginalNumber,
    Guid RegisterId,
    Guid SessionId,
    Guid? CustomerPartyId,
    string CustomerName,
    DateTimeOffset ReturnDate,
    decimal SubTotal,
    decimal TaxTotal,
    decimal TotalAmount,
    string Reason,
    string CashierName,
    Guid? CreditNoteId,
    string? CreditNoteNumber,
    List<PosReturnLineDto> Lines,
    List<PosReturnRefundDto> Refunds)
{
    internal static PosReturnDto From(PosReturn r) => new(
        r.Id, r.Number, r.LegalEntityId, r.OriginalOrderId, r.OriginalNumber, r.RegisterId, r.SessionId, r.CustomerPartyId, r.CustomerName, r.ReturnDate,
        r.SubTotal, r.TaxTotal, r.TotalAmount, r.Reason, r.CashierName, r.CreditNoteId, r.CreditNoteNumber,
        r.Lines.Select(l => new PosReturnLineDto(l.Id, l.OrderLineId, l.ItemId, l.ItemCode, l.Description, l.Quantity, l.UnitPrice, l.DiscountPercent, l.TaxRate, l.NetAmount, l.TaxAmount, l.LineTotal, l.Restock)).ToList(),
        r.Refunds.Select(f => new PosReturnRefundDto(f.Id, f.Method, f.Amount, f.Reference)).ToList());
}
