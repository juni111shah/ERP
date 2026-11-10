using System.Linq.Expressions;
using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

public record PosSessionSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    Guid RegisterId,
    string RegisterName,
    Guid OpenedByUserId,
    string CashierName,
    DateTimeOffset OpenedAt,
    Guid? ClosedByUserId,
    DateTimeOffset? ClosedAt,
    decimal OpeningCash,
    decimal TotalSales,
    decimal TotalReturns,
    decimal ExpectedCash,
    decimal ActualCash,
    decimal CashDifference,
    PosSessionStatus Status,
    string? Notes,
    DateTimeOffset CreatedAt,
    decimal TotalPayIn = 0,
    decimal TotalPayOut = 0)
{
    /// <summary>The list row, projected in SQL.</summary>
    public static readonly Expression<Func<PosSession, PosSessionSummaryDto>> Projection = s => new PosSessionSummaryDto(
        s.Id, s.LegalEntityId, s.RegisterId, s.RegisterName, s.OpenedByUserId, s.CashierName,
        s.OpenedAt, s.ClosedByUserId, s.ClosedAt, s.OpeningCash, s.TotalSales, s.TotalReturns,
        s.ExpectedCash, s.ActualCash, s.CashDifference, s.Status, s.Notes, s.CreatedAt, s.TotalPayIn, s.TotalPayOut);
}
