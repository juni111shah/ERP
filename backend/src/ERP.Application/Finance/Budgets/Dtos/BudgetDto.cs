using System.Linq.Expressions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public sealed record BudgetLineDto(Guid AccountId, string AccountCode, string AccountName, int Month, decimal Amount);
public sealed record BudgetDto(Guid Id, Guid LegalEntityId, string Name, int FiscalYear, string? Notes, bool IsActive, decimal TotalIncome, decimal TotalExpense, IReadOnlyList<BudgetLineDto> Lines);

public sealed record BudgetSummaryDto(Guid Id, Guid LegalEntityId, string Name, int FiscalYear, bool IsActive)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<Budget, BudgetSummaryDto>> Projection = b => new BudgetSummaryDto(b.Id, b.LegalEntityId, b.Name, b.FiscalYear, b.IsActive);
}
