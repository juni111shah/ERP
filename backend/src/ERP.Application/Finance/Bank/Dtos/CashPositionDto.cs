namespace ERP.Application.Finance;

public sealed record CashPositionRow(Guid AccountId, string Code, string Name, bool IsBank, decimal Balance);

/// <summary>The balance of every bank and cash account as of a date.</summary>
public sealed record CashPositionDto(DateOnly AsOf, IReadOnlyList<CashPositionRow> Accounts, decimal Total);
