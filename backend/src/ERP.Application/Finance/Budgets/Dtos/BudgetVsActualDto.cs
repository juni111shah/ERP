using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public sealed record BudgetVsActualRow(Guid AccountId, string Code, string Name, AccountType Type, decimal Budget, decimal Actual, decimal Variance, decimal? VariancePercent);
public sealed record BudgetVsActualDto(Guid BudgetId, string Name, int FiscalYear, int FromMonth, int ToMonth, IReadOnlyList<BudgetVsActualRow> Rows,
    decimal BudgetIncome, decimal ActualIncome, decimal BudgetExpense, decimal ActualExpense, decimal BudgetNet, decimal ActualNet);
