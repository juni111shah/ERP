namespace ERP.Application.Finance;

public sealed record JournalLineInput(Guid AccountId, decimal Debit, decimal Credit, string? Description = null, Guid? PartyId = null, Guid? BusinessUnitId = null);
public sealed record JournalInput(Guid LegalEntityId, DateOnly Date, string? Memo, string? Reference, List<JournalLineInput> Lines);
