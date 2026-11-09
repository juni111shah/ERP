using System.Linq.Expressions;
using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

public record PosOrderSummaryDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid RegisterId,
    Guid SessionId,
    Guid? CustomerPartyId,
    string CustomerName,
    DateTimeOffset OrderDate,
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    PosOrderStatus Status,
    PosPaymentStatus PaymentStatus,
    string CashierName,
    int LineCount,
    DateTimeOffset CreatedAt,
    decimal ReturnedTotal = 0,
    string? InvoiceNumber = null)
{
    /// <summary>The list row, projected in SQL.</summary>
    public static readonly Expression<Func<PosOrder, PosOrderSummaryDto>> Projection = o => new PosOrderSummaryDto(
        o.Id, o.Number, o.LegalEntityId, o.RegisterId, o.SessionId, o.CustomerPartyId,
        o.CustomerName, o.OrderDate, o.SubTotal, o.TaxTotal, o.DiscountTotal,
        o.TotalAmount, o.Status, o.PaymentStatus, o.CashierName, o.Lines.Count, o.CreatedAt, o.ReturnedTotal, o.InvoiceNumber);
}
