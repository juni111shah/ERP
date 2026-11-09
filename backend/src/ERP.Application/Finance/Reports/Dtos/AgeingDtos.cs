namespace ERP.Application.Finance;

public sealed record AgeingRow(Guid PartyId, string? PartyName, decimal Current, decimal Days1To30, decimal Days31To60, decimal Days61To90, decimal Over90, decimal Total);
public sealed record AgeingItem(Guid InvoiceId, string Number, bool IsCreditNote, DateOnly InvoiceDate, DateOnly DueDate, int DaysOverdue, decimal Outstanding);

/// <summary>Receivables (or payables) by party in buckets of days past due, with the totals row.</summary>
public sealed record ArAgeingDto(DateOnly AsOf, IReadOnlyList<AgeingRow> Rows, AgeingRow Totals);

public sealed record CustomerStatementDto(Guid PartyId, string? PartyName, DateOnly AsOf, IReadOnlyList<AgeingItem> Items, decimal UnallocatedReceipts, decimal Balance);
public sealed record VendorStatementDto(Guid PartyId, string? PartyName, DateOnly AsOf, IReadOnlyList<AgeingItem> Items, decimal UnallocatedPayments, decimal Balance);
