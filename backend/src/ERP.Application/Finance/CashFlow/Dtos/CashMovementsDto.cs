namespace ERP.Application.Finance;

public sealed record CashMovementRow(Guid AccountId, string Code, string Name, decimal Opening, decimal Inflows, decimal Outflows, decimal Closing);
public sealed record CashSourceRow(string Source, decimal Inflows, decimal Outflows);
public sealed record CashMovementsDto(DateOnly From, DateOnly To, IReadOnlyList<CashMovementRow> Accounts, IReadOnlyList<CashSourceRow> BySource, decimal Opening, decimal Inflows, decimal Outflows, decimal Closing);
