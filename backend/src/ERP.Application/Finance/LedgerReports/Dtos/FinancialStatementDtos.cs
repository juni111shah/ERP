namespace ERP.Application.Finance;

public sealed record StatementLine(string Group, string Code, string Name, decimal Amount);
public sealed record IncomeStatementDto(DateOnly From, DateOnly To, IReadOnlyList<StatementLine> Income, IReadOnlyList<StatementLine> Expenses, decimal TotalIncome, decimal TotalExpenses, decimal NetProfit);
public sealed record BalanceSheetDto(
    DateOnly AsOf, IReadOnlyList<StatementLine> Assets, IReadOnlyList<StatementLine> Liabilities, IReadOnlyList<StatementLine> Equity,
    decimal TotalAssets, decimal TotalLiabilities, decimal TotalEquity, decimal CurrentEarnings, bool Balanced, decimal PriorOpenEarnings = 0m);
