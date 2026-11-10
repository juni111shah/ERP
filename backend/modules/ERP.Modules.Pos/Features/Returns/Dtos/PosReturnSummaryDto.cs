using System.Linq.Expressions;
using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

public record PosReturnSummaryDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid OriginalOrderId,
    string OriginalNumber,
    Guid RegisterId,
    Guid SessionId,
    string CustomerName,
    DateTimeOffset ReturnDate,
    decimal SubTotal,
    decimal TaxTotal,
    decimal TotalAmount,
    string Reason,
    string CashierName,
    string? CreditNoteNumber)
{
    /// <summary>The list row, projected in SQL.</summary>
    public static readonly Expression<Func<PosReturn, PosReturnSummaryDto>> Projection = r => new PosReturnSummaryDto(
        r.Id, r.Number, r.LegalEntityId, r.OriginalOrderId, r.OriginalNumber, r.RegisterId, r.SessionId, r.CustomerName,
        r.ReturnDate, r.SubTotal, r.TaxTotal, r.TotalAmount, r.Reason, r.CashierName, r.CreditNoteNumber);
}
