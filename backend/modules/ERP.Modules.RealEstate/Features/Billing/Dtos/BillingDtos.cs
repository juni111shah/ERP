namespace ERP.Modules.RealEstate.Features;

public sealed record BillingResult(int Invoiced, decimal TotalBilled, IReadOnlyList<Guid> InvoiceIds);
public sealed record InstalmentInvoiceDto(Guid InstalmentId, Guid InvoiceId, string Number, decimal Total);

/// <summary>Lease, property, unit and service period behind invoices raised by the billing run, so the core invoice list can show them without knowing about leases.</summary>
public sealed record InvoiceLeaseLinkDto(Guid InvoiceId, Guid LeaseId, string? LeaseNumber, Guid PropertyId, string? PropertyName, string? UnitNumber, DateOnly PeriodFrom, DateOnly PeriodTo);
