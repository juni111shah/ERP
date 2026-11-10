namespace ERP.Application.Finance;

public sealed record LedgerLineDto(Guid JournalId, string Number, DateOnly Date, string? Memo, string? Description, decimal Debit, decimal Credit, decimal Balance);
public sealed record GeneralLedgerDto(Guid AccountId, string Code, string Name, DateOnly? From, DateOnly To, decimal Opening, IReadOnlyList<LedgerLineDto> Lines, decimal Closing);
