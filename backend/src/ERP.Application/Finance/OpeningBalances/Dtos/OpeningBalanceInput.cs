namespace ERP.Application.Finance;

/// <summary>One balance at go-live. On the receivables and payables control accounts each line is one open document of a party (with its own due date and reference).</summary>
public sealed record OpeningBalanceLineInput(Guid AccountId, decimal Debit, decimal Credit, Guid? PartyId = null, string? Description = null, Guid? BusinessUnitId = null, DateOnly? DueDate = null, string? Reference = null);
public sealed record OpeningBalanceInput(Guid LegalEntityId, DateOnly Date, List<OpeningBalanceLineInput> Lines, string? Memo = null);
