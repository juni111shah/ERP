using System.Linq.Expressions;
using ERP.Modules.Sales.Domain;

namespace ERP.Modules.Sales.Features;

public sealed record SalesReturnSummaryDto(
    Guid Id, string Number, Guid LegalEntityId, Guid? SalesOrderId, string? OrderNumber, Guid? InvoiceId, string? InvoiceNumber, Guid CustomerPartyId, string CustomerName,
    DateOnly ReturnDate, SalesReturnStatus Status, Guid? CreditNoteId, string? CreditNoteNumber, decimal TotalAmount, int LineCount, DateTime CreatedAt)
{
    /// <summary>The list row, projected in SQL.</summary>
    public static readonly Expression<Func<SalesReturn, SalesReturnSummaryDto>> Projection = r => new SalesReturnSummaryDto(
        r.Id, r.Number, r.LegalEntityId, r.SalesOrderId, r.OrderNumber, r.InvoiceId, r.InvoiceNumber, r.CustomerPartyId, r.CustomerName,
        r.ReturnDate, r.Status, r.CreditNoteId, r.CreditNoteNumber, r.TotalAmount, r.Lines.Count, r.CreatedAt);
}
