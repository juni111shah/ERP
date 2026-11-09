namespace ERP.Modules.Services.Features;

/// <summary>
/// What a period of a retainer invoiced: the recurring fee plus overage for the approved billable hours above the included hours. AlreadyInvoiced says this call found the period's
/// earlier invoice instead of raising a new one. UnapprovedHours are hours dated in the period that were not billed because nobody has approved them yet.
/// </summary>
public sealed record RetainerInvoiceDto(
    Guid RetainerId, DateOnly PeriodStart, DateOnly PeriodEnd, decimal RecurringFee, decimal IncludedHours, decimal BillableHours, decimal OverageHours, decimal OverageHourlyRate, decimal OverageAmount,
    Guid InvoiceId, string InvoiceNumber, decimal Net, decimal Tax, decimal Total, bool AlreadyInvoiced, decimal UnapprovedHours);
