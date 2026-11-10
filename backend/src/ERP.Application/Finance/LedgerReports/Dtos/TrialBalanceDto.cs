using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public sealed record TrialBalanceRow(Guid AccountId, string Code, string Name, AccountType Type, string? ReportingGroup, decimal Opening, decimal Debit, decimal Credit, decimal Closing);
public sealed record TrialBalanceDto(DateOnly? From, DateOnly To, IReadOnlyList<TrialBalanceRow> Rows, decimal TotalDebit, decimal TotalCredit, bool Balanced);
