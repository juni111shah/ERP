using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Services.Features;

/// <summary>
/// Raises the draft invoice of one retainer period (any date inside the period names it; none means the period containing today). Idempotent: asking again returns the same invoice;
/// voiding it frees the period. Approved time that arrives after its period was invoiced is billed with the next period.
/// </summary>
public sealed record GenerateRetainerInvoiceCommand(Guid RetainerId, DateOnly? PeriodDate = null, DateOnly? InvoiceDate = null, DateOnly? DueDate = null, Guid? TaxCodeId = null, string? Notes = null)
    : ICommand<RetainerInvoiceDto>, IRequirePermission { public string RequiredPermission => Permissions.RetainersManage; }
