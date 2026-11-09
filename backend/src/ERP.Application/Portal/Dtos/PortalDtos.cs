using ERP.Domain.Finance;

namespace ERP.Application.Portal;

public sealed record PortalProfileDto(Guid PartyId, string Name, string? Email, string? Phone);
public sealed record PortalInvoiceDto(Guid Id, string Number, bool IsCreditNote, DateOnly InvoiceDate, DateOnly DueDate, string CurrencyCode, decimal Total, decimal Paid, decimal Outstanding, DocumentStatus Status);
public sealed record PortalReceiptDto(Guid Id, string Number, DateOnly Date, SettlementMethod Method, string CurrencyCode, decimal Amount, string? Reference);
public sealed record PortalStatementLine(string Number, bool IsCreditNote, DateOnly InvoiceDate, DateOnly DueDate, int DaysOverdue, decimal Outstanding);
public sealed record PortalStatementDto(DateOnly AsOf, IReadOnlyList<PortalStatementLine> Items, decimal UnallocatedReceipts, decimal Balance);

public sealed record PortalBillDto(Guid Id, string Number, string? VendorReference, DateOnly BillDate, DateOnly DueDate, string CurrencyCode, decimal Total, decimal Paid, decimal Outstanding, DocumentStatus Status);
public sealed record PortalPaymentDto(Guid Id, string Number, DateOnly Date, SettlementMethod Method, string CurrencyCode, decimal Amount, string? Reference);
public sealed record PortalPayablesLine(string Number, DateOnly BillDate, DateOnly DueDate, int DaysOverdue, decimal Outstanding);
public sealed record PortalPayablesDto(DateOnly AsOf, IReadOnlyList<PortalPayablesLine> Items, decimal Balance);
