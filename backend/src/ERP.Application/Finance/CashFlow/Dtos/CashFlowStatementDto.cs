namespace ERP.Application.Finance;

public enum CashFlowCategory { Operating, Investing, Financing }

public sealed record CashFlowLine(string Group, string Code, string Name, decimal Inflows, decimal Outflows, decimal Net);
public sealed record CashFlowSection(CashFlowCategory Category, IReadOnlyList<CashFlowLine> Lines, decimal Inflows, decimal Outflows, decimal Net);

/// <summary>
/// Direct-method statement. <see cref="OpeningBalance"/> + <see cref="NetChange"/> must equal the bank and cash balance in the ledger at the end of the
/// period (<see cref="LedgerClosingBalance"/>); <see cref="TiesOut"/> says it does. <see cref="InternalTransfers"/> is money moved between the entity's own bank
/// and cash accounts: it is shown for information and is not a cash flow.
/// </summary>
public sealed record CashFlowStatementDto(
    DateOnly From, DateOnly To, Guid? LegalEntityId, string? CurrencyCode, decimal OpeningBalance, IReadOnlyList<CashFlowSection> Sections, decimal NetChange, decimal ClosingBalance,
    decimal LedgerClosingBalance, bool TiesOut, decimal InternalTransfers);
